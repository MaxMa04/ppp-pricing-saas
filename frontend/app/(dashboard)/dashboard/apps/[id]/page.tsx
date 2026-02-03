"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { ArrowLeft, ChevronRight, TrendingUp } from "lucide-react";

export default function AppDetailPage() {
  const params = useParams();
  const id = params.id as string;

  const [app, setApp] = useState<any>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchApp = async () => {
      try {
        const api = createApi(getCurrentUserToken);
        const data = await api.apps.get(id);
        setApp(data);
      } catch (error) {
        console.error("Failed to fetch app:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchApp();
  }, [id]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  if (!app) {
    return (
      <div className="text-center py-12">
        <p className="text-muted-foreground">App not found</p>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div className="flex items-center gap-4">
        <Link href="/dashboard/apps">
          <Button variant="ghost" size="icon">
            <ArrowLeft className="h-5 w-5" />
          </Button>
        </Link>
        <div className="flex items-center gap-4">
          {app.iconUrl ? (
            <img
              src={app.iconUrl}
              alt={app.appName}
              className="h-16 w-16 rounded-xl"
            />
          ) : (
            <div className="flex h-16 w-16 items-center justify-center rounded-xl bg-muted">
              <TrendingUp className="h-8 w-8 text-muted-foreground" />
            </div>
          )}
          <div>
            <h1 className="text-3xl font-bold">{app.appName}</h1>
            <p className="text-muted-foreground">
              {app.packageName || app.bundleId || app.appStoreId}
            </p>
          </div>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Subscriptions</CardTitle>
        </CardHeader>
        <CardContent>
          {app.subscriptions?.length === 0 ? (
            <p className="text-center text-muted-foreground py-8">
              No subscriptions found. Sync your app to import subscriptions.
            </p>
          ) : (
            <div className="divide-y">
              {app.subscriptions?.map((sub: any) => (
                <Link
                  key={sub.id}
                  href={`/dashboard/apps/${app.id}/subscriptions/${sub.id}`}
                  className="flex items-center justify-between py-4 hover:bg-muted/50 -mx-4 px-4 rounded-lg transition-colors"
                >
                  <div>
                    <div className="font-medium">{sub.name || sub.productId}</div>
                    <div className="text-sm text-muted-foreground">
                      {sub.productId}
                      {sub.basePlanId && ` / ${sub.basePlanId}`}
                      {sub.billingPeriod && ` - ${sub.billingPeriod}`}
                    </div>
                  </div>
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <span>{sub.priceCount} regions</span>
                    <ChevronRight className="h-4 w-4" />
                  </div>
                </Link>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
