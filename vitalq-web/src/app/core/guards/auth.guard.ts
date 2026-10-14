/**
 * authGuard: Functional route guard enforcing authentication and role-based access control.
 * Unauthenticated users are redirected to /login; unauthorized roles are redirected to their default portal.
 */

import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

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
