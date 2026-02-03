const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";
const DEFAULT_TIMEOUT = 10000; // 10 seconds

type FetchOptions = RequestInit & {
  token?: string | null;
  timeout?: number;
};

export async function apiClient<T>(
  endpoint: string,
  options: FetchOptions = {}
): Promise<T> {
  const { token, timeout = DEFAULT_TIMEOUT, ...fetchOptions } = options;

  const headers: HeadersInit = {
    "Content-Type": "application/json",
    ...(options.headers || {}),
  };

  if (token) {
    (headers as Record<string, string>)["Authorization"] = `Bearer ${token}`;
  }

  // Add request timeout using AbortController
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeout);

  try {
    const response = await fetch(`${API_URL}${endpoint}`, {
      ...fetchOptions,
      headers,
      signal: controller.signal,
    });

    if (response.status === 204) {
      return {} as T;
    }

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.message || error.error || `HTTP ${response.status}`);
    }

    return response.json();
  } catch (error) {
    if (error instanceof Error && error.name === "AbortError") {
      throw new Error("Request timeout. Please try again.");
    }
    throw error;
  } finally {
    clearTimeout(timeoutId);
  }
}

export function createApi(getToken: () => Promise<string | null>) {
  const request = async <T>(
    endpoint: string,
    options: Omit<FetchOptions, "token"> = {}
  ): Promise<T> => {
    const token = await getToken();
    return apiClient<T>(endpoint, { ...options, token });
  };

  return {
    // Auth
    auth: {
      verify: () => request<{ id: string; email: string; displayName: string }>("/api/auth/verify", { method: "POST" }),
    },

    // Connections
    connections: {
      list: () => request<any[]>("/api/connections"),
      delete: (id: string) => request(`/api/connections/${id}`, { method: "DELETE" }),
    },

    // Google Play
    googlePlay: {
      getAuthUrl: () => request<{ url: string; state: string }>("/api/google-play/auth/url"),
      callback: (code: string, state?: string) =>
        request<{ success: boolean; connectionId: string }>("/api/google-play/auth/callback", {
          method: "POST",
          body: JSON.stringify({ code, state }),
        }),
      getApps: () => request<any[]>("/api/google-play/apps"),
      getAvailableApps: () =>
        request<{ apps: Array<{ packageName: string; name: string; iconUrl?: string; alreadyImported: boolean }>; note: string }>("/api/google-play/available-apps"),
      importApps: (packageNames: string[]) =>
        request<{ imported: number; apps: Array<{ id: string; packageName: string; name: string; alreadyExisted: boolean }>; errors: Array<{ packageName: string; error: string }> }>("/api/google-play/import-apps", {
          method: "POST",
          body: JSON.stringify({ packageNames }),
        }),
      syncApp: (packageName: string) =>
        request(`/api/google-play/apps/${packageName}/sync`, { method: "POST" }),
      syncSubscriptions: (packageName: string) =>
        request<{ success: boolean; subscriptionCount: number; priceCount: number; subscriptions: any[] }>(
          `/api/google-play/apps/${packageName}/sync-subscriptions`,
          { method: "POST", timeout: 60000 }
        ),
    },

    // App Store
    appStore: {
      connect: (keyId: string, issuerId: string, privateKey: string) =>
        request<{ success: boolean; connectionId: string }>("/api/app-store/connect", {
          method: "POST",
          body: JSON.stringify({ keyId, issuerId, privateKey }),
        }),
      getApps: () => request<any[]>("/api/app-store/apps"),
      getAvailableApps: () =>
        request<Array<{ appStoreId: string; bundleId: string; name: string; sku?: string }>>("/api/app-store/available-apps"),
      importApps: (appIds: string[]) =>
        request<{ imported: number; apps: Array<{ id: string; name: string; alreadyExisted: boolean }> }>("/api/app-store/import-apps", {
          method: "POST",
          body: JSON.stringify({ appIds }),
        }),
      syncApp: (appStoreId: string) =>
        request(`/api/app-store/apps/${appStoreId}/sync`, { method: "POST" }),
      syncSubscriptions: (appStoreId: string) =>
        request<{ success: boolean; subscriptionCount: number; priceCount: number; subscriptions: any[] }>(
          `/api/app-store/apps/${appStoreId}/sync-subscriptions`,
          { method: "POST", timeout: 120000 }
        ),
    },

    // Subscriptions
    subscriptions: {
      get: (id: string) => request<any>(`/api/subscriptions/${id}`),
      getPrices: (id: string) => request<any[]>(`/api/subscriptions/${id}/prices`),
      previewPrices: (id: string) =>
        request<{
          subscription: { id: string; name: string; productId: string };
          summary: { increases: number; decreases: number; unchanged: number; total: number };
          prices: Array<{
            regionCode: string;
            currencyCode: string;
            currentPrice: number | null;
            suggestedPrice: number | null;
            multiplier: number;
            change: number | null;
          }>;
        }>(`/api/subscriptions/${id}/prices/preview`, { method: "POST" }),
      applyPrices: (id: string) =>
        request<{
          success: boolean;
          appliedCount: number;
          failedCount: number;
          changes: Array<{
            regionCode: string;
            oldPrice: number;
            newPrice: number;
            status: string;
            errorMessage?: string;
          }>;
        }>(`/api/subscriptions/${id}/prices/apply`, { method: "POST", timeout: 120000 }),
      getPriceHistory: (id: string) => request<any[]>(`/api/subscriptions/${id}/prices/history`),
    },

    // PPP
    ppp: {
      getMultipliers: (indexType?: string) =>
        request<any[]>(`/api/ppp/multipliers${indexType ? `?indexType=${indexType}` : ""}`),
      getMultiplier: (regionCode: string, indexType?: string) =>
        request<any>(`/api/ppp/multipliers/${regionCode}${indexType ? `?indexType=${indexType}` : ""}`),
      updateMultiplier: (regionCode: string, data: { multiplier: number; countryName?: string; source?: string }) =>
        request(`/api/ppp/multipliers/${regionCode}`, {
          method: "PUT",
          body: JSON.stringify(data),
        }),
      importMultipliers: (data: Array<{ regionCode: string; multiplier: number; countryName?: string; source?: string }>) =>
        request<{ imported: number; updated: number; total: number }>("/api/ppp/multipliers/import", {
          method: "POST",
          body: JSON.stringify(data),
        }),
      importBigMac: () =>
        request<{ success: boolean; imported: number; updated: number; total: number; dataDate?: string }>("/api/ppp/import/bigmac", {
          method: "POST",
        }),
      importNetflix: (planType: string = "standard") =>
        request<{ success: boolean; imported: number; updated: number; total: number; dataDate?: string }>(`/api/ppp/import/netflix?planType=${planType}`, {
          method: "POST",
        }),
      importWages: () =>
        request<{ success: boolean; imported: number; updated: number; total: number; dataDate?: string }>("/api/ppp/import/wages", {
          method: "POST",
        }),
      calculateWorkingHours: () =>
        request<{ success: boolean; imported: number; updated: number; total: number; dataDate?: string }>("/api/ppp/import/working-hours", {
          method: "POST",
        }),
      getRawData: (indexType: string) =>
        request<any[]>(`/api/ppp/raw-data/${indexType}`),
      getIndexTypes: () =>
        request<Array<{ value: number; name: string; displayName: string }>>("/api/ppp/index-types"),
    },

    // Apps
    apps: {
      list: () => request<any[]>("/api/apps"),
      get: (id: string) => request<any>(`/api/apps/${id}`),
      delete: (id: string) => request(`/api/apps/${id}`, { method: "DELETE" }),
      getSubscriptions: (id: string) => request<any[]>(`/api/apps/${id}/subscriptions`),
      updatePreferredIndex: (id: string, indexType: number) =>
        request<{ id: string; preferredIndexType: string; preferredIndexTypeValue: number }>(`/api/apps/${id}/preferred-index`, {
          method: "PUT",
          body: JSON.stringify({ preferredIndexType: indexType }),
        }),
    },
  };
}
