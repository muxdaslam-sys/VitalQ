import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SkipPatientModalComponent } from './skip-patient-modal.component';
import { QueueToken } from '../../../../shared/models/queue.model';

describe('SkipPatientModalComponent', () => {
  let component: SkipPatientModalComponent;
  let fixture: ComponentFixture<SkipPatientModalComponent>;

  const createMockToken = (skipCount?: number): QueueToken => ({
    id: 'tok-123',
    tokenNumber: 'CARD-101',
    patientId: 'pat-1',
    patientName: 'John Doe',
    patientPhone: '1234567890',
    departmentId: 'dept-1',
    departmentName: 'Cardiology',
    departmentCode: 'CARD',
    doctorId: 'doc-1',
    doctorName: 'Dr. Sarah',
    roomNumber: '201',
    status: 'Called',
    priorityScore: 500,
    baseWeight: 500,
    patientsAhead: 0,
    estimatedWaitMinutes: 0,
    skipCount: skipCount,
    bookedAtUtc: new Date().toISOString()
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SkipPatientModalComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(SkipPatientModalComponent);
    component = fixture.componentInstance;
  });

  it('should calculate strike 1 for fresh token without previous skips', () => {
    component.currentCalled = createMockToken(undefined);
    fixture.detectChanges();

    expect(component.nextStrike()).toBe(1);
  });

  it('should calculate strike 2 after 1 prior skip', () => {
    component.currentCalled = createMockToken(1);
    fixture.detectChanges();

    expect(component.nextStrike()).toBe(2);
  });

  it('should calculate strike 3 after 2 prior skips (final permanent no-show)', () => {
    component.currentCalled = createMockToken(2);
    fixture.detectChanges();

    expect(component.nextStrike()).toBe(3);
  });

  it('should emit confirm event when confirmed and not submitting', () => {
    component.currentCalled = createMockToken(0);
    fixture.detectChanges();

    spyOn(component.confirm, 'emit');
    component.isSubmitting = false;
    component.onConfirm();

    expect(component.confirm.emit).toHaveBeenCalled();
  });

  it('should not emit confirm event when isSubmitting is true', () => {
    component.currentCalled = createMockToken(0);
    fixture.detectChanges();

    spyOn(component.confirm, 'emit');
    component.isSubmitting = true;
    component.onConfirm();

    expect(component.confirm.emit).not.toHaveBeenCalled();
  });
});
