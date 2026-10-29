/**
 * SignalRService: Manages persistent WebSocket connection to the SignalR Hub (/hubs/queue)
 * and exposes reactive RxJS event streams for real-time queue synchronization.
 */

import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { QueueToken } from '../../shared/models/queue.model';

@Injectable({ providedIn: 'root' })
export class SignalRService {
  /** Underlying Microsoft SignalR connection instance */
  private hubConnection: signalR.HubConnection | null = null;

  // --------------------------------------------------------------------------
  // REACTIVE EVENT STREAMS (RxJS Subjects)
  // Components subscribe to these observables to update their Angular Signals.
  // --------------------------------------------------------------------------
  private tokenBookedSubject = new Subject<QueueToken>();
  private queueUpdatedSubject = new Subject<QueueToken>();
  private patientCalledSubject = new Subject<QueueToken>();
  private triageCompletedSubject = new Subject<QueueToken>();
  private scoresRecalculatedSubject = new Subject<string>();
  private doctorStatusChangedSubject = new Subject<{ doctorId: string; status: string }>();

  /** Emitted whenever any patient books a new token in a monitored department */
  public onTokenBooked$: Observable<QueueToken> = this.tokenBookedSubject.asObservable();

  /** Emitted whenever triage vitals are recorded, a patient is skipped, or queue moves */
  public onQueueUpdated$: Observable<QueueToken> = this.queueUpdatedSubject.asObservable();

  /** Emitted whenever a doctor presses "Call Next" in their consultation room */
  public onPatientCalled$: Observable<QueueToken> = this.patientCalledSubject.asObservable();

  /** Emitted whenever triage vitals are completed for a token */
  public onTriageCompleted$: Observable<QueueToken> = this.triageCompletedSubject.asObservable();

  /** Emitted by the backend AgingWorker background service (every 60s) */
  public onScoresRecalculated$: Observable<string> = this.scoresRecalculatedSubject.asObservable();

  /** Emitted whenever a physician's duty status (Available, Busy, OnLeave) is updated */
  public onDoctorStatusChanged$: Observable<{ doctorId: string; status: string }> = this.doctorStatusChangedSubject.asObservable();

  /** Boolean flag tracking live WebSocket connection health */
  public isConnected = false;

  /**
   * Initializes and starts the persistent WebSocket connection to the SignalR Hub.
   * Transmits JWT bearer token via `accessTokenFactory` for group authorization.
   */
  startConnection(): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return Promise.resolve();
    }

    // DYNAMIC ACCESS TOKEN FACTORY (HIGH-RESILIENCY):
    // Evaluating `sessionStorage.getItem('vq_token')` inside the callback lambda ensures
    // that whenever SignalR reconnects after intermittent hospital Wi-Fi drops,
    // it transmits the freshly rotated 60-minute JWT bearer token instead of a stale token.
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.signalrUrl, {
        accessTokenFactory: () => sessionStorage.getItem('vq_token') || localStorage.getItem('vq_token') || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerServerEvents();

    return this.hubConnection
      .start()
      .then(() => {
        this.isConnected = true;
      })
      .catch((err) => {
        this.isConnected = false;
        console.warn('SignalR Connection Error:', err);
      });
  }

  /**
   * Cleanly closes the WebSocket connection during user logout or teardown.
   */
  stopConnection(): Promise<void> {
    if (this.hubConnection) {
      return this.hubConnection.stop().then(() => {
        this.isConnected = false;
      });
    }
    return Promise.resolve();
  }

  // --------------------------------------------------------------------------
  // ROOM & GROUP TARGETING
  // Subscribes a client socket to targeted broadcast groups.
  // --------------------------------------------------------------------------

  /** Joins real-time updates for a specific doctor's room queue */
  async joinDoctorGroup(doctorId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinDoctorGroup', doctorId.toLowerCase());
    }
  }

  /** Leaves a doctor's room queue broadcast */
  async leaveDoctorGroup(doctorId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveDoctorGroup', doctorId.toLowerCase());
    }
  }

  /** Joins real-time updates for the nurse triage desk station */
  async joinNurseStationGroup(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinNurseStationGroup');
    }
  }

  /** Leaves the nurse triage desk broadcast group */
  async leaveNurseStationGroup(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveNurseStationGroup');
    }
  }

  /** Joins real-time hospital administration KPI overview group */
  async joinAdminGroup(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinAdminGroup');
    }
  }

  /** Leaves the administration broadcast group */
  async leaveAdminGroup(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveAdminGroup');
    }
  }

  /** Joins real-time updates for a specific clinical department (e.g. Cardiology OPD) */
  async joinDepartmentGroup(deptId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinDepartmentGroup', deptId.toLowerCase());
    }
  }

  /** Leaves a department's broadcast group */
  async leaveDepartmentGroup(deptId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveDepartmentGroup', deptId.toLowerCase());
    }
  }

  /** Subscribes a patient to targeted alerts for their specific consultation slip */
  async joinTokenGroup(tokenId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinTokenGroup', tokenId.toLowerCase());
    }
  }

  /** Unsubscribes from a specific token group upon completion or cancellation */
  async leaveTokenGroup(tokenId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveTokenGroup', tokenId.toLowerCase());
    }
  }

  /**
   * Registers hub method listeners pushing events into local RxJS subjects.
   */
  private registerServerEvents(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('TokenBooked', (token: QueueToken) => {
      this.tokenBookedSubject.next(token);
    });

    this.hubConnection.on('QueueUpdated', (token: QueueToken) => {
      this.queueUpdatedSubject.next(token);
    });

    this.hubConnection.on('PatientCalled', (token: QueueToken) => {
      this.patientCalledSubject.next(token);
    });

    this.hubConnection.on('TriageCompleted', (token: QueueToken) => {
      this.triageCompletedSubject.next(token);
    });

    this.hubConnection.on('QueueScoresRecalculated', (timestamp: string) => {
      this.scoresRecalculatedSubject.next(timestamp);
    });

    this.hubConnection.on('DoctorStatusChanged', (data: { doctorId: string; status: string }) => {
      this.doctorStatusChangedSubject.next(data);
    });
  }
}
