/**
 * ============================================================================
 * GUARD: authGuard (Role-Based Access Control & Navigation Protection)
 * ============================================================================
 * 
 * PURPOSE:
 * Enforces route authorization. Unauthenticated users are redirected to `/login`
 * with a preserved `returnUrl`. Authenticated users attempting to navigate to
 * unassigned portals are rerouted to their authorized landing page.
 * ============================================================================
 */

import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

//#region Route Guard Factory

/**
 * Higher-order functional route guard accepting an array of authorized roles.
 * 
 * @param allowedRoles Array of acceptable roles, e.g. ['Admin'], ['Doctor'], ['Patient']
 * @example canActivate: [authGuard(['Admin'])]
 */
export const authGuard = (allowedRoles: string[] = []): CanActivateFn => {
  return (route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    // 1. Enforce authentication
    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    }

    // 2. Enforce role authorization
    const currentRole = auth.userRole();
    if (allowedRoles.length > 0 && currentRole && !allowedRoles.includes(currentRole)) {
      // Reroute to their respective authorized home screen
      return router.createUrlTree([auth.getRoleDefaultRoute(currentRole)]);
    }

    // 3. User is authorized
    return true;
  };
};

//#endregion
