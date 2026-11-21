/**
 * بازیابی نشست در boot — silent refresh با Cookie HttpOnly.
 * Access token در حافظه است و با reload از بین می‌رود؛ نشست با refresh برمی‌گردد.
 * ضد-حلقه: فقط یک تلاش؛ در صورت شکست کاربر به login هدایت می‌شود.
 */
import { useAuthStore } from '@/core/auth/auth.store';
import { getCurrentUser, refreshSession } from '@/core/auth/auth.service';

export interface SessionRestoreResult {
  restored: boolean;
  /** به /login برو یا /select-company؟ */
  nextRoute: 'app' | 'select-company' | 'login';
}

let restorePromise: Promise<SessionRestoreResult> | null = null;

export function restoreSession(): Promise<SessionRestoreResult> {
  // mutex: هم‌زمانی چند caller (React StrictMode) → یک تلاش مشترک
  if (restorePromise) return restorePromise;

  restorePromise = (async (): Promise<SessionRestoreResult> => {
    const store = useAuthStore.getState();
    if (store.isAuthenticated && store.accessToken) {
      return { restored: true, nextRoute: 'app' };
    }

    try {
      const session = await refreshSession();
      // فاز ۱۰ — یافته ممیزی: me باید پس از ست‌شدن access token صدا بخورد، وگرنه
      // بدون Bearer می‌رود و با backend واقعی 401 → خروج در هر reload.
      useAuthStore.getState().setSession(session.accessToken, session.tenantId, session.companyId, session.permissions);
      const user = await getCurrentUser();
      store.setAuth(user, session.accessToken, session.tenantId, session.companyId);
      return { restored: true, nextRoute: 'select-company' };
    } catch {
      useAuthStore.getState().clearAuth();
      return { restored: false, nextRoute: 'login' };
    }
  })();

  restorePromise.finally(() => {
    restorePromise = null;
  });

  return restorePromise;
}
