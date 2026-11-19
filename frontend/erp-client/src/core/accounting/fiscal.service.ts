/** سرویس سال/دوره مالی — endpointهای واقعی: /api/v1/accounting/fiscal-years */
import { apiClient } from '@/core/api/client';
import type {
  CreateCustomPeriodRequest,
  CreateFiscalYearRequest,
  FiscalPeriodDto,
  FiscalYearDto,
  GeneratePeriodsRequest,
  PeriodGenerationType,
} from './fiscal.types';

const BASE = '/FiscalYears';

export async function getAllFiscalYears(): Promise<FiscalYearDto[]> {
  const { data } = await apiClient.get<FiscalYearDto[]>(BASE);
  return data ?? [];
}

export async function getFiscalYear(id: string): Promise<FiscalYearDto> {
  const { data } = await apiClient.get<FiscalYearDto>(`${BASE}/${id}`);
  return data;
}

export async function createFiscalYear(request: CreateFiscalYearRequest): Promise<FiscalYearDto> {
  const { data } = await apiClient.post<FiscalYearDto>(BASE, request);
  return data;
}

export async function openFiscalYear(id: string): Promise<FiscalYearDto> {
  const { data } = await apiClient.post<FiscalYearDto>(`${BASE}/${id}/open`);
  return data;
}

export async function closeFiscalYear(id: string): Promise<FiscalYearDto> {
  const { data } = await apiClient.post<FiscalYearDto>(`${BASE}/${id}/close`);
  return data;
}

export async function getFiscalPeriods(id: string): Promise<FiscalPeriodDto[]> {
  const { data } = await apiClient.get<FiscalPeriodDto[]>(`${BASE}/${id}/periods`);
  return data ?? [];
}

export async function generatePeriods(id: string, generationType: PeriodGenerationType): Promise<FiscalPeriodDto[]> {
  const request: GeneratePeriodsRequest = { generationType };
  const { data } = await apiClient.post<FiscalPeriodDto[]>(`${BASE}/${id}/periods/generate`, request);
  return data ?? [];
}

export async function createCustomPeriod(id: string, request: CreateCustomPeriodRequest): Promise<FiscalPeriodDto> {
  const { data } = await apiClient.post<FiscalPeriodDto>(`${BASE}/${id}/periods/custom`, request);
  return data;
}

export async function openPeriod(fiscalYearId: string, periodId: string): Promise<FiscalPeriodDto> {
  const { data } = await apiClient.post<FiscalPeriodDto>(`${BASE}/${fiscalYearId}/periods/${periodId}/open`);
  return data;
}

export async function closePeriod(fiscalYearId: string, periodId: string): Promise<FiscalPeriodDto> {
  const { data } = await apiClient.post<FiscalPeriodDto>(`${BASE}/${fiscalYearId}/periods/${periodId}/close`);
  return data;
}
