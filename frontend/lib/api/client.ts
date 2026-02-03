const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

type FetchOptions = RequestInit & {
  token?: string | null;
};

export async function apiClient<T>(
  endpoint: string,
  options: FetchOptions = {}
): Promise<T> {
  const { token, ...fetchOptions } = options;

  const headers: HeadersInit = {
    "Content-Type": "application/json",
    ...(options.headers || {}),
  };

  if (token) {
    (headers as Record<string, string>)["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_URL}${endpoint}`, {
    ...fetchOptions,
    headers,
  });

  if (response.status === 204) {
    return {} as T;
  }

  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(error.message || error.error || `HTTP ${response.status}`);
  }

  return response.json();
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
      syncApp: (packageName: string) =>
        request(`/api/google-play/apps/${packageName}/sync`, { method: "POST" }),
    },

    // App Store
    appStore: {
      connect: (keyId: string, issuerId: string, privateKey: string) =>
        request<{ success: boolean; connectionId: string }>("/api/app-store/connect", {
          method: "POST",
          body: JSON.stringify({ keyId, issuerId, privateKey }),
        }),
      getApps: () => request<any[]>("/api/app-store/apps"),
      syncApp: (appStoreId: string) =>
        request(`/api/app-store/apps/${appStoreId}/sync`, { method: "POST" }),
    },

    // Apps
    apps: {
      list: () => request<any[]>("/api/apps"),
      get: (id: string) => request<any>(`/api/apps/${id}`),
      getSubscriptions: (id: string) => request<any[]>(`/api/apps/${id}/subscriptions`),
    },

    // Subscriptions
    subscriptions: {
      get: (id: string) => request<any>(`/api/subscriptions/${id}`),
      getPrices: (id: string) => request<any[]>(`/api/subscriptions/${id}/prices`),
      previewPrices: (id: string) =>
        request<any>(`/api/subscriptions/${id}/prices/preview`, { method: "POST" }),
      applyPrices: (id: string) =>
        request<any>(`/api/subscriptions/${id}/prices/apply`, { method: "POST" }),
      getPriceHistory: (id: string) => request<any[]>(`/api/subscriptions/${id}/prices/history`),
    },

    // PPP
    ppp: {
      getMultipliers: () => request<any[]>("/api/ppp/multipliers"),
      getMultiplier: (regionCode: string) => request<any>(`/api/ppp/multipliers/${regionCode}`),
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
    },
  };
}
