import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { SignalRService } from './signalr.service';

describe('AuthService', () => {
  let service: AuthService;
  let mockHttp: jasmine.SpyObj<HttpClient>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockSignalR: jasmine.SpyObj<SignalRService>;

  beforeEach(() => {
    mockHttp = jasmine.createSpyObj('HttpClient', ['post', 'get']);
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockSignalR = jasmine.createSpyObj('SignalRService', ['startConnection', 'stopConnection']);

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        { provide: HttpClient, useValue: mockHttp },
        { provide: Router, useValue: mockRouter },
        { provide: SignalRService, useValue: mockSignalR }
      ]
    });

    sessionStorage.clear();
    service = TestBed.inject(AuthService);
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('should return correct role default routes', () => {
    expect(service.getRoleDefaultRoute('Admin')).toBe('/admin/dashboard');
    expect(service.getRoleDefaultRoute('Doctor')).toBe('/doctor');
    expect(service.getRoleDefaultRoute('Nurse')).toBe('/nurse');
    expect(service.getRoleDefaultRoute('Patient')).toBe('/patient');
    expect(service.getRoleDefaultRoute('Unknown')).toBe('/login');
  });

  it('should identify empty token as expired', () => {
    expect(service.isTokenExpired()).toBeTrue();
  });

  it('should decode active unexpired token correctly', () => {
    // Construct valid unexpired JWT payload with exp 1 hour in the future
    const futureExp = Math.floor(Date.now() / 1000) + 3600;
    const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
    const payload = btoa(JSON.stringify({ sub: '123', exp: futureExp }));
    const mockJwt = `${header}.${payload}.signature`;

    sessionStorage.setItem('vq_token', mockJwt);
    expect(service.isTokenExpired()).toBeFalse();
  });

  it('should identify expired token correctly', () => {
    // Construct expired JWT payload
    const pastExp = Math.floor(Date.now() / 1000) - 3600;
    const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
    const payload = btoa(JSON.stringify({ sub: '123', exp: pastExp }));
    const mockJwt = `${header}.${payload}.signature`;

    sessionStorage.setItem('vq_token', mockJwt);
    expect(service.isTokenExpired()).toBeTrue();
  });
});
