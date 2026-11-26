import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { IdleTimeoutService } from './idle-timeout.service';
import { AuthService } from './auth.service';

describe('IdleTimeoutService', () => {
  let service: IdleTimeoutService;
  let mockAuth: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    mockAuth = jasmine.createSpyObj('AuthService', ['isAuthenticated', 'logout']);
    mockAuth.logout.and.returnValue(of(undefined as any));

    TestBed.configureTestingModule({
      providers: [
        IdleTimeoutService,
        { provide: AuthService, useValue: mockAuth }
      ]
    });

    service = TestBed.inject(IdleTimeoutService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should not log out when unauthenticated on timeout', fakeAsync(() => {
    mockAuth.isAuthenticated.and.returnValue(false);

    service.startWatching();
    // Advance time past 15 minutes
    tick(15 * 60 * 1000 + 1000);

    expect(mockAuth.logout).not.toHaveBeenCalled();
  }));

  it('should trigger logout when authenticated session reaches 15m inactivity', fakeAsync(() => {
    mockAuth.isAuthenticated.and.returnValue(true);

    service.startWatching();
    // Advance time past 15 minutes (900,000 ms)
    tick(15 * 60 * 1000 + 100);

    expect(mockAuth.logout).toHaveBeenCalled();
  }));
});
