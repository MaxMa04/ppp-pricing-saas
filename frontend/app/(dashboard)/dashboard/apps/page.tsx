"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { AppWindow, ChevronRight } from "lucide-react";

export default function AppsPage() {
  const [apps, setApps] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchApps = async () => {
      try {
        const api = createApi(getCurrentUserToken);
        const data = await api.apps.list();
        setApps(data);
      } catch (error) {
        console.error("Failed to fetch apps:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchApps();
  }, []);

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
        <Link href="/dashboard/connections">
          <Button>Connect Store</Button>
        </Link>
      </div>

      {apps.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <AppWindow className="h-12 w-12 text-muted-foreground" />
            <h3 className="mt-4 text-lg font-medium">No apps yet</h3>
            <p className="mt-2 text-center text-sm text-muted-foreground">
              Connect your App Store or Google Play account to import your apps.
            </p>
            <Link href="/dashboard/connections" className="mt-4">
              <Button>Connect Store</Button>
            </Link>
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
