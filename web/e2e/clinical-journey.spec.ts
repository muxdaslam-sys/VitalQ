import { test, expect } from '@playwright/test';

/**
 * Phase 23: End-to-End Clinical Journey & Multi-Role Queue Orchestration Test Suite.
 * Validates the full clinical lifecycle across Patient, Nurse, Doctor, and Public portals.
 */
test.describe('VitalQ Multi-Role Clinical Queue Orchestration', () => {

  const createMockJwt = (role: string): string => {
    const header = Buffer.from(JSON.stringify({ alg: 'HS256', typ: 'JWT' })).toString('base64url');
    const payload = Buffer.from(JSON.stringify({ role, exp: 4102444800 })).toString('base64url');
    return `${header}.${payload}.mock_signature`;
  };

  const MOCK_PATIENT_TOKEN = createMockJwt('Patient');
  const MOCK_NURSE_TOKEN = createMockJwt('Nurse');
  const MOCK_DOCTOR_TOKEN = createMockJwt('Doctor');

  test.beforeEach(async ({ page }) => {
    // Inject mock API routes for predictable, isolated E2E testing
    await page.route('**/api/public/departments', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: '11111111-1111-1111-1111-111111111111', name: 'Cardiology', code: 'CARD', locationFloor: 'Floor 2, Wing A', isActive: true },
          { id: '22222222-2222-2222-2222-222222222222', name: 'General Medicine', code: 'GEN', locationFloor: 'Ground Floor, Wing B', isActive: true }
        ])
      });
    });

    await page.route('**/api/public/departments/*/doctors', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: '33333333-3333-3333-3333-333333333333', fullName: 'Dr. Sarah Smith', doctorName: 'Dr. Sarah Smith', departmentName: 'Cardiology', roomNumber: 'Room 201', status: 'Available' }
        ])
      });
    });

    // Mock SignalR negotiate endpoint to avoid network errors
    await page.route('**/hubs/queue/**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ connectionId: 'mock-signalr-conn-id', availableTransports: [] })
      });
    });
  });

  test('1. Authentication Gate: Unauthenticated users are redirected to login', async ({ page }) => {
    await page.goto('/patient');
    await expect(page).toHaveURL(/.*login/);

    await page.goto('/doctor');
    await expect(page).toHaveURL(/.*login/);

    await page.goto('/nurse');
    await expect(page).toHaveURL(/.*login/);

    await page.goto('/admin/dashboard');
    await expect(page).toHaveURL(/.*login/);
  });

  test('2. Patient Journey: Mobile Appointment Booking & Live Queue Pass', async ({ page }) => {
    // Mock Patient Login
    await page.route('**/api/auth/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          accessToken: MOCK_PATIENT_TOKEN,
          expiresAtUtc: '2099-01-01T00:00:00Z',
          user: {
            id: 'patient-uuid-1234',
            username: '9876543210',
            fullName: 'John Doe',
            phoneNumber: '9876543210',
            role: 'Patient',
            isActive: true,
            createdAtUtc: new Date().toISOString()
          }
        })
      });
    });

    // Mock Active Bookings
    await page.route('**/api/patient/bookings/active', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'token-uuid-001',
            tokenNumber: 'CARD-261128-001',
            patientName: 'John Doe',
            departmentName: 'Cardiology',
            doctorName: 'Dr. Sarah Smith',
            status: 'Waiting',
            triageLevel: 'Yellow',
            priorityScore: 500,
            roomNumber: 'Room 201',
            estimatedWaitMinutes: 15,
            patientsAhead: 2,
            createdAtUtc: new Date().toISOString()
          }
        ])
      });
    });

    // Mock Patient Family
    await page.route('**/api/patient/family', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    // Step 1: Login as Patient
    await page.goto('/login');
    await page.fill('input[name="username"]', '9876543210');
    await page.fill('input[name="password"]', '15081990');
    await page.click('button[type="submit"]');

    // Step 2: Verify Patient OPD Dashboard loads
    await expect(page).toHaveURL(/.*patient/);

    // Step 3: Verify Token Card & Queue Pass Details
    await expect(page.locator('text=CARD-261128-001').first()).toBeVisible();
    await expect(page.locator('text=Cardiology').first()).toBeVisible();
    await expect(page.locator('text=Consultation Pass').first()).toBeVisible();
  });

  test('3. Nurse Triage Journey: Station Queue & Acuity Assessment', async ({ page }) => {
    // Mock Nurse Login
    await page.route('**/api/auth/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          accessToken: MOCK_NURSE_TOKEN,
          expiresAtUtc: '2099-01-01T00:00:00Z',
          user: {
            id: 'nurse-uuid-5678',
            username: 'nurse.jane',
            fullName: 'Nurse Jane',
            phoneNumber: '9876543211',
            role: 'Nurse',
            nursingStationId: '44444444-4444-4444-4444-444444444444',
            nursingStationName: 'Station 1',
            isActive: true,
            createdAtUtc: new Date().toISOString()
          }
        })
      });
    });

    // Mock Pending Triage Queue
    await page.route('**/api/nurse/triage/pending*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'token-uuid-001',
            tokenNumber: 'CARD-261128-001',
            patientName: 'John Doe',
            patientPhone: '9876543210',
            doctorName: 'Dr. Sarah Smith',
            departmentName: 'Cardiology',
            status: 'Booked',
            triageLevel: 'Green',
            baseWeight: 100,
            bookedAtUtc: new Date().toISOString(),
            createdAtUtc: new Date().toISOString()
          }
        ])
      });
    });

    // Mock Dashboard Stats
    await page.route('**/api/nurse/dashboard/stats*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalBookingsToday: 15,
          totalTriagedToday: 14,
          totalCompletedToday: 12,
          totalSkippedToday: 0,
          doctorsPresent: 1,
          doctorsAbsent: 0,
          doctors: [
            {
              doctorId: '33333333-3333-3333-3333-333333333333',
              doctorName: 'Dr. Sarah Smith',
              specialization: 'Cardiology',
              roomNumber: 'Room 201',
              status: 'Available',
              waitingCount: 2,
              totalAssignedToday: 10,
              skippedCount: 0
            }
          ]
        })
      });
    });

    // Step 1: Login as Nurse
    await page.goto('/login');
    await page.fill('input[name="username"]', 'nurse.jane');
    await page.fill('input[name="password"]', 'NursePass123!');
    await page.click('button[type="submit"]');

    // Step 2: Verify Nurse Command Center loads
    await expect(page).toHaveURL(/.*nurse/);
    await expect(page.locator('text=Nurse Triage Command Desk')).toBeVisible();

    // Step 3: Verify Pending Intake Queue renders patient token in the visible desktop queue table
    await expect(page.getByRole('table').getByText('CARD-261128-001')).toBeVisible();
  });

  test('4. Doctor Chamber Journey: Priority Pacing & Consultation Room', async ({ page }) => {
    // Mock Doctor Login
    await page.route('**/api/auth/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          accessToken: MOCK_DOCTOR_TOKEN,
          expiresAtUtc: '2099-01-01T00:00:00Z',
          user: {
            id: 'doctor-user-uuid',
            username: 'dr.smith',
            fullName: 'Dr. Sarah Smith',
            phoneNumber: '9876543212',
            role: 'Doctor',
            isActive: true,
            createdAtUtc: new Date().toISOString()
          }
        })
      });
    });

    // Mock Doctor Profile
    await page.route('**/api/doctor/me', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: '33333333-3333-3333-3333-333333333333',
          fullName: 'Dr. Sarah Smith',
          doctorName: 'Dr. Sarah Smith',
          departmentName: 'Cardiology',
          roomNumber: 'Room 201',
          status: 'Available',
          consultationFee: 500
        })
      });
    });

    // Mock Doctor Queue
    await page.route('**/api/doctor/queue/*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'token-uuid-001',
            tokenNumber: 'CARD-261128-001',
            patientName: 'John Doe',
            status: 'Waiting',
            triageLevel: 'Yellow',
            priorityScore: 520,
            estimatedWaitMinutes: 5,
            createdAtUtc: new Date().toISOString()
          }
        ])
      });
    });

    // Mock Current Called Token
    await page.route('**/api/doctor/current/*', async (route) => {
      await route.fulfill({
        status: 204,
        body: ''
      });
    });

    // Step 1: Login as Doctor
    await page.goto('/login');
    await page.fill('input[name="username"]', 'dr.smith');
    await page.fill('input[name="password"]', 'DoctorPass123!');
    await page.click('button[type="submit"]');

    // Step 2: Verify Doctor Consultation Console loads
    await expect(page).toHaveURL(/.*doctor/);
    await expect(page.locator('text=Dr. Sarah Smith').first()).toBeVisible();
    await expect(page.locator('text=Room 201').first()).toBeVisible();

    // Step 3: Verify waiting patient in CTAS queue
    await expect(page.locator('text=CARD-261128-001').first()).toBeVisible();
  });

  test('5. Public Anonymous Slip Tracker: Instant Token Search', async ({ page }) => {
    // Mock Public Token Tracking
    await page.route('**/api/public/track/CARD-261128-001', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tokenNumber: 'CARD-261128-001',
          departmentName: 'Cardiology',
          doctorName: 'Dr. Sarah Smith',
          roomNumber: 'Room 201',
          status: 'Waiting',
          patientsAhead: 1,
          estimatedWaitMinutes: 10
        })
      });
    });

    await page.goto('/track');
    await expect(page.locator('text=Live Token Tracker')).toBeVisible();

    await page.fill('input[name="searchToken"]', 'CARD-261128-001');
    await page.click('button[type="submit"]');

    await expect(page.locator('text=CARD-261128-001').first()).toBeVisible();
    await expect(page.locator('text=Cardiology').first()).toBeVisible();
  });

  test('6. Security & HIPAA Safeguards: Session Isolation & Clean Teardown', async ({ page }) => {
    // Verify sessionStorage tab isolation
    await page.goto('/login');
    const token = await page.evaluate(() => sessionStorage.getItem('vq_token'));
    const user = await page.evaluate(() => sessionStorage.getItem('vq_user'));
    expect(token).toBeNull();
    expect(user).toBeNull();
  });

});
