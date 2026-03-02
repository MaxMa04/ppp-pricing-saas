"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { AppWindow, ChevronRight, AlertCircle, Plus } from "lucide-react";

export default function AppsPage() {
  const [apps, setApps] = useState<any[]>([]);
  const [connections, setConnections] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const api = createApi(getCurrentUserToken);
        const [appsData, connectionsData] = await Promise.all([
          api.apps.list(),
          api.connections.list(),
        ]);
        setApps(appsData);
        setConnections(connectionsData);
      } catch (err) {
        const message = err instanceof Error ? err.message : "Unknown error";
        console.error("Failed to fetch apps:", message);
        setError(message);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, []);

  const hasConnectedStore = connections.length > 0;

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Apps</h1>
          <p className="text-muted-foreground">
            Manage your connected applications
          </p>
        </div>
        <div className="flex gap-2">
          {hasConnectedStore && (
            <Link href="/dashboard/connections/import-apps">
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                Add App
              </Button>
            </Link>
          )}
          <Link href="/dashboard">
            <Button variant={hasConnectedStore ? "outline" : "default"}>
              {hasConnectedStore ? "Manage Stores" : "Connect Store"}
            </Button>
          </Link>
        </div>
      </div>

      {error && (
        <Card className="border-destructive">
          <CardContent className="flex items-center gap-4 py-4">
            <AlertCircle className="h-6 w-6 text-destructive" />
            <div>
              <p className="font-medium text-destructive">Failed to load apps</p>
              <p className="text-sm text-muted-foreground">{error}</p>
            </div>
          </CardContent>
        </Card>
      )}

      {!error && apps.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <AppWindow className="h-12 w-12 text-muted-foreground" />
            <h3 className="mt-4 text-lg font-medium">No apps yet</h3>
            <p className="mt-2 text-center text-sm text-muted-foreground">
              Connect your App Store or Google Play account to import your apps.
            </p>
            <div className="mt-4 flex gap-2">
              <Link href="/dashboard">
                <Button variant="outline">Connect Store</Button>
              </Link>
              <Link href="/dashboard/connections/import-apps">
                <Button>Import Apps</Button>
              </Link>
            </div>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {apps.map((app) => (
            <Link key={app.id} href={`/dashboard/apps/${app.id}`}>
              <Card className="cursor-pointer transition-shadow hover:shadow-md">
                <CardHeader className="flex flex-row items-center gap-4">
                  {app.iconUrl ? (
                    <img
                      src={app.iconUrl}
                      alt={app.appName}
                      className="h-12 w-12 rounded-lg"
                    />
                  ) : (
                    <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-muted">
                      <AppWindow className="h-6 w-6 text-muted-foreground" />
                    </div>
                  )}
                  <div className="flex-1">
                    <CardTitle className="text-base">{app.appName}</CardTitle>
                    <p className="text-sm text-muted-foreground">
                      {app.packageName || app.bundleId || app.appStoreId}
                    </p>
                  </div>
                  <ChevronRight className="h-5 w-5 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-muted-foreground">
                      {app.storeType === 0 ? "Google Play" : "App Store"}
                    </span>
                    <span>
                      {app.subscriptionCount}{" "}
                      {app.subscriptionCount === 1 ? "subscription" : "subscriptions"}
                    </span>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
