import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authGuard = (allowedRoles: string[] = []): CanActivateFn => {
  return (route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    }

    const currentRole = auth.userRole();
    if (allowedRoles.length > 0 && currentRole && !allowedRoles.includes(currentRole)) {
      // Redirect to their respective authorized home
      return router.createUrlTree([auth.getRoleDefaultRoute(currentRole)]);
    }

    return true;
  };
};
