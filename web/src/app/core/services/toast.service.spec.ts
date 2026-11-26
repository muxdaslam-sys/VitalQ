import { TestBed } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ToastService]
    });
    service = TestBed.inject(ToastService);
  });

  it('should initialize with empty toast list', () => {
    expect(service.toasts().length).toBe(0);
  });

  it('should append a success toast with correct attributes', () => {
    service.success('Token booked successfully', 'Booking Confirmed');
    const toasts = service.toasts();

    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('success');
    expect(toasts[0].message).toBe('Token booked successfully');
    expect(toasts[0].title).toBe('Booking Confirmed');
    expect(toasts[0].duration).toBe(3500);
  });

  it('should append an error toast with 5000ms duration', () => {
    service.error('Failed to connect to server', 'Connection Error');
    const toasts = service.toasts();

    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('error');
    expect(toasts[0].message).toBe('Failed to connect to server');
    expect(toasts[0].title).toBe('Connection Error');
    expect(toasts[0].duration).toBe(5000);
  });

  it('should append warning and info toasts', () => {
    service.warning('Mandatory fields missing');
    service.info('Copied to clipboard');

    const toasts = service.toasts();
    expect(toasts.length).toBe(2);
    expect(toasts[0].type).toBe('warning');
    expect(toasts[1].type).toBe('info');
  });

  it('should remove a toast by ID', () => {
    service.info('First Toast');
    service.info('Second Toast');

    const initialToasts = service.toasts();
    expect(initialToasts.length).toBe(2);

    const firstId = initialToasts[0].id;
    service.remove(firstId);

    const remainingToasts = service.toasts();
    expect(remainingToasts.length).toBe(1);
    expect(remainingToasts[0].message).toBe('Second Toast');
  });
});
