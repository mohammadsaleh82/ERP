/**
 * مجوزهای واقعی Backend — منبع: docs/development/permission-matrix.md
 * (src/ERP.BuildingBlocks/Security/AccountingPermissions.cs)
 * هر رشته دقیقاً مطابق Policy سمت سرور است؛ frontend فقط نمایش/دسترس‌پذیری را کنترل می‌کند
 * و Backend همچنان مرجع نهایی اعمال مجوز است.
 */

/** مجوزهای حاکمیتی خارج از ماتریس حسابداری (از TenantResolutionMiddleware) */
export const SYSTEM_ADMIN = 'system:admin' as const;

export const PERMISSIONS = {
  // حساب‌ها
  accountsRead: 'accounting:accounts:read',
  accountsCreate: 'accounting:accounts:create',
  accountsUpdate: 'accounting:accounts:update',
  accountsDeactivate: 'accounting:accounts:deactivate',
  accountsDelete: 'accounting:accounts:delete',
  // اسناد
  documentsRead: 'accounting:documents:read',
  documentsCreate: 'accounting:documents:create',
  documentsUpdate: 'accounting:documents:update',
  documentsDelete: 'accounting:documents:delete',
  documentsSubmit: 'accounting:documents:submit',
  documentsApprove: 'accounting:documents:approve',
  documentsReject: 'accounting:documents:reject',
  documentsPost: 'accounting:documents:post',
  documentsReverse: 'accounting:documents:reverse',
  // دفتر و گزارش
  ledgerRead: 'accounting:ledger:read',
  reportsRead: 'accounting:reports:read',
  dimensionsRead: 'accounting:dimensions:read',
  auditRead: 'accounting:audit:read',
  // سال/دوره مالی
  fiscalYearsCreate: 'accounting:fiscal-years:create',
  fiscalYearsRead: 'accounting:fiscal-years:read',
  fiscalYearsUpdate: 'accounting:fiscal-years:update',
  periodClose: 'accounting:periods:close',
  fiscalYearClose: 'accounting:fiscalyear:close',
  fiscalYearReopen: 'accounting:fiscalyear:reopen',
  settingsRead: 'accounting:settings:read',
  settingsUpdate: 'accounting:settings:update',
} as const;

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS] | string;

/** عملیات خطری که همیشه تأیید صریح می‌خواهند — نقشه عملیات به مجوز */
export const ACTION_PERMISSIONS = {
  view: 'read',
  create: 'create',
  update: 'update',
  delete: 'delete',
  submit: 'submit',
  approve: 'approve',
  reject: 'reject',
  post: 'post',
  reverse: 'reverse',
} as const;

export type CrudAction = keyof typeof ACTION_PERMISSIONS;

/** رشته مجوز استاندارد `<module>:<resource>:<action>` */
export const buildPermission = (module: string, resource: string, action: CrudAction): string =>
  `${module}:${resource}:${ACTION_PERMISSIONS[action]}`;
