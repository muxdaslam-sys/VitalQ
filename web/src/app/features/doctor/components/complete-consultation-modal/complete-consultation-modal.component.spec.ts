import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CompleteConsultationModalComponent } from './complete-consultation-modal.component';
import { QueueToken } from '../../../../shared/models/queue.model';

describe('CompleteConsultationModalComponent', () => {
  let component: CompleteConsultationModalComponent;
  let fixture: ComponentFixture<CompleteConsultationModalComponent>;

  const mockToken: QueueToken = {
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
    bookedAtUtc: new Date().toISOString()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompleteConsultationModalComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CompleteConsultationModalComponent);
    component = fixture.componentInstance;
    component.currentCalled = mockToken;
    fixture.detectChanges();
  });

  it('should create modal component with initial values', () => {
    expect(component).toBeTruthy();
    expect(component.consultationNotes()).toBe('');
    expect(component.isSubmitting).toBeFalse();
  });

  it('should not emit complete when notes are less than 5 characters', () => {
    spyOn(component.complete, 'emit');

    component.consultationNotes.set('abc'); // only 3 chars
    component.onSubmit();

    expect(component.complete.emit).not.toHaveBeenCalled();
  });

  it('should not emit complete when isSubmitting is true', () => {
    spyOn(component.complete, 'emit');

    component.consultationNotes.set('Patient discharged with low-salt diet prescription.');
    component.isSubmitting = true;
    component.onSubmit();

    expect(component.complete.emit).not.toHaveBeenCalled();
  });

  it('should emit trimmed notes when valid and >= 5 characters', () => {
    spyOn(component.complete, 'emit');

    const validNotes = '   Patient diagnosed with mild hypertension. Prescribed ACE inhibitor.   ';
    component.consultationNotes.set(validNotes);
    component.isSubmitting = false;
    component.onSubmit();

    expect(component.complete.emit).toHaveBeenCalledWith(validNotes.trim());
  });
});
