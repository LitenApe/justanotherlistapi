import axios, { AxiosError, AxiosInstance, AxiosRequestConfig } from 'axios';

export type HttpResponse<T = any> = {
  ok: boolean;
  status: number;
  data?: T;
  error?: any; // parsed object or raw string
};

export class HttpError<T = any> extends Error {
  response: HttpResponse<T>;
  constructor(message: string, response: HttpResponse<T>) {
    super(message);
    this.name = 'HttpError';
    this.response = response;
  }
}

type ClientOptions = {
  baseURL?: string;
  timeout?: number;
  headers?: Record<string, string>;
};

class AxiosHttpClient {
  private instance: AxiosInstance;

  constructor(opts?: ClientOptions) {
    // always request as text so we can keep the raw body and attempt JSON.parse ourselves
    this.instance = axios.create({
      baseURL: opts?.baseURL ?? '',
      timeout: opts?.timeout ?? 10_000,
      headers: opts?.headers,
      responseType: 'text',
    });
  }

  private parseRaw<T = any>(raw: unknown): { parsed?: T; raw?: string } {
    if (raw == null) return { parsed: undefined, raw: undefined };
    if (typeof raw !== 'string') {
      // sometimes axios may already give parsed non-string — keep it as parsed
      return { parsed: raw as any, raw: undefined };
    }
    const rawStr = raw as string;
    if (rawStr === '') return { parsed: undefined, raw: undefined };
    try {
      return { parsed: JSON.parse(rawStr) as T, raw: rawStr };
    } catch {
      return { parsed: undefined, raw: rawStr };
    }
  }

  private buildErrorResponseFromAxios(err: AxiosError): HttpResponse {
    const status = err.response?.status ?? 0;
    const raw = err.response?.data;
    const { parsed, raw: rawText } = this.parseRaw(raw);
    const payload = parsed ?? rawText ?? { message: err.message };
    return {
      ok: false,
      status,
      error: payload,
    };
  }

  async request<T = any>(config: AxiosRequestConfig): Promise<HttpResponse<T>> {
    try {
      const res = await this.instance.request<string>({
        ...config,
        responseType: 'text',
      });
      const { parsed, raw } = this.parseRaw<T>(res.data);
      const payload =
        parsed ??
        (raw as any) ??
        (res.statusText ? { message: res.statusText } : undefined);

      const result: HttpResponse<T> = {
        ok: true,
        status: res.status,
        data: payload as T,
      };

      return result;
    } catch (error: unknown) {
      if (axios.isAxiosError(error)) {
        // axios throws for non-2xx and network errors
        const resp = this.buildErrorResponseFromAxios(error);
        const message =
          typeof resp.error === 'string'
            ? resp.error
            : resp.error?.message ?? `HTTP ${resp.status}`;
        throw new HttpError(message, resp);
      }

      // unknown non-axios error
      const message = (error as any)?.message ?? String(error);
      const resp: HttpResponse = { ok: false, status: 0, error: { message } };
      throw new HttpError(message, resp);
    }
  }

  get<T = any>(url: string, config?: AxiosRequestConfig) {
    return this.request<T>({ method: 'GET', url, ...config });
  }
  post<T = any>(url: string, data?: any, config?: AxiosRequestConfig) {
    return this.request<T>({ method: 'POST', url, data, ...config });
  }
  put<T = any>(url: string, data?: any, config?: AxiosRequestConfig) {
    return this.request<T>({ method: 'PUT', url, data, ...config });
  }
  del<T = any>(url: string, config?: AxiosRequestConfig) {
    return this.request<T>({ method: 'DELETE', url, ...config });
  }
}

export const http = new AxiosHttpClient({ baseURL: '' });
