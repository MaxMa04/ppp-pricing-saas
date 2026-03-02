"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { ArrowLeft, ChevronRight, TrendingUp, Settings, Loader2, Trash2, RefreshCw } from "lucide-react";
import { toast } from "sonner";

const INDEX_OPTIONS = [
  { value: 0, label: "Big Mac Index", description: "Classic PPP indicator" },
  { value: 1, label: "Netflix Index", description: "Digital goods PPP" },
  { value: 2, label: "Working Hours", description: "Purchasing power in work time" },
];

const NETFLIX_PLAN_OPTIONS = [
  { value: "mobile", label: "Mobile" },
  { value: "basic", label: "Basic" },
  { value: "standard", label: "Standard" },
  { value: "premium", label: "Premium" },
];

export default function AppDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [app, setApp] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [updatingIndex, setUpdatingIndex] = useState(false);
  const [showIndexDropdown, setShowIndexDropdown] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [syncing, setSyncing] = useState(false);

  useEffect(() => {
    const fetchApp = async () => {
      try {
        const api = createApi(getCurrentUserToken);
        const data = await api.apps.get(id);
        setApp(data);
      } catch (error) {
        console.error("Failed to fetch app:", error instanceof Error ? error.message : "Unknown error");
      } finally {
        setLoading(false);
      }
    };

    fetchApp();
  }, [id]);

  const ensurePppData = async (api: ReturnType<typeof createApi>, indexType: number, planType?: string) => {
    const indexTypeMap: Record<number, string> = { 0: "BigMac", 1: "Netflix", 2: "BigMacWorkingHours" };
    try {
      const data = await api.ppp.getMultipliers(indexTypeMap[indexType], planType);
      if (data.length === 0) {
        toast.info("Loading PPP data...");
        if (indexType === 0) {
          await api.ppp.importBigMac();
        } else if (indexType === 1) {
          await api.ppp.importNetflix(planType);
        } else {
          await api.ppp.importWages();
          await api.ppp.calculateWorkingHours();
        }
        toast.success("PPP data loaded");
      }
    } catch (error) {
      console.error("Failed to ensure PPP data:", error);
    }
  };

  const handleIndexChange = async (indexType: number, preferredNetflixPlan?: string) => {
    setUpdatingIndex(true);
    try {
      const api = createApi(getCurrentUserToken);
      const result = await api.apps.updatePreferredIndex(id, indexType, preferredNetflixPlan);
      setApp((prev: any) => ({
        ...prev,
        preferredIndexType: result.preferredIndexType,
        preferredIndexTypeValue: result.preferredIndexTypeValue,
        preferredNetflixPlan: result.preferredNetflixPlan,
      }));
      setShowIndexDropdown(false);
      toast.success(`Pricing index updated to ${INDEX_OPTIONS.find((o) => o.value === indexType)?.label}`);

      // Auto-fetch PPP data if not available
      ensurePppData(api, indexType, preferredNetflixPlan);
    } catch (error) {
      console.error("Failed to update index:", error);
      toast.error("Failed to update pricing index");
    } finally {
      setUpdatingIndex(false);
    }
  };

  const handleDelete = async () => {
    if (!confirm(`Are you sure you want to delete "${app.appName}"? This will also delete all subscriptions and price data.`)) {
      return;
    }

    setDeleting(true);
    try {
      const api = createApi(getCurrentUserToken);
      await api.apps.delete(id);
      toast.success("App deleted successfully");
      router.push("/dashboard/apps");
    } catch (error) {
      console.error("Failed to delete app:", error);
      toast.error("Failed to delete app");
      setDeleting(false);
    }
  };

  const handleSyncSubscriptions = async () => {
    setSyncing(true);
    try {
      const api = createApi(getCurrentUserToken);
      let result;

      if (app.storeType === 0) {
        // Google Play
        result = await api.googlePlay.syncSubscriptions(app.packageName);
      } else {
        // App Store
        result = await api.appStore.syncSubscriptions(app.appStoreId);
      }

      if (result.success) {
        const baseMessage = `Synced ${result.subscriptionCount} subscriptions with ${result.priceCount} regional prices`;
        const removedMessage =
          result.deletedSubscriptionCount > 0 || result.deletedPriceCount > 0
            ? ` Removed ${result.deletedSubscriptionCount} subscriptions and ${result.deletedPriceCount} prices.`
            : "";
        const metadataMessage = result.metadataSynced ? " App metadata synced." : " App metadata unchanged.";
        const snapshotMessage = result.isFullSnapshot ? "" : " Partial upstream snapshot: stale entries were not deleted.";

        toast.success(`${baseMessage}.${removedMessage}${metadataMessage}${snapshotMessage}`);
        // Refresh app data
        const updatedApp = await api.apps.get(id);
        setApp(updatedApp);
      } else {
        toast.error("Failed to sync app data");
      }
    } catch (error) {
      console.error("Failed to sync app data:", error);
      toast.error(error instanceof Error ? error.message : "Failed to sync app data");
    } finally {
      setSyncing(false);
    }
  };

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

  const currentIndex = INDEX_OPTIONS.find((o) => o.value === app.preferredIndexTypeValue) || INDEX_OPTIONS[0];

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

      {/* Pricing Index Selection */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Settings className="h-5 w-5" />
                Pricing Index
              </CardTitle>
              <CardDescription>
                Choose which PPP index to use for price calculations
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="relative">
            <button
              onClick={() => setShowIndexDropdown(!showIndexDropdown)}
              disabled={updatingIndex}
              className="w-full flex items-center justify-between p-4 border rounded-lg hover:bg-muted/50 transition-colors"
            >
              <div className="text-left">
                <div className="font-medium">{currentIndex.label}</div>
                <div className="text-sm text-muted-foreground">{currentIndex.description}</div>
              </div>
              {updatingIndex ? (
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              ) : (
                <ChevronRight className={`h-5 w-5 text-muted-foreground transition-transform ${showIndexDropdown ? "rotate-90" : ""}`} />
              )}
            </button>

            {showIndexDropdown && !updatingIndex && (
              <div className="absolute top-full left-0 right-0 mt-2 border rounded-lg bg-background shadow-lg z-10">
                {INDEX_OPTIONS.map((option) => (
                  <button
                    key={option.value}
                    onClick={() => handleIndexChange(option.value, app.preferredNetflixPlan || "standard")}
                    className={`w-full flex items-center justify-between p-4 hover:bg-muted/50 transition-colors first:rounded-t-lg last:rounded-b-lg ${
                      option.value === app.preferredIndexTypeValue ? "bg-muted/30" : ""
                    }`}
                  >
                    <div className="text-left">
                      <div className="font-medium">{option.label}</div>
                      <div className="text-sm text-muted-foreground">{option.description}</div>
                    </div>
                    {option.value === app.preferredIndexTypeValue && (
                      <span className="text-xs bg-primary text-primary-foreground px-2 py-1 rounded">Current</span>
                    )}
                  </button>
                ))}
              </div>
            )}
          </div>

          {app.preferredIndexTypeValue === 1 && (
            <div className="mt-4 border rounded-lg p-4">
              <div className="text-sm font-medium mb-3">Netflix plan for multiplier calculation</div>
              <div className="flex flex-wrap gap-2">
                {NETFLIX_PLAN_OPTIONS.map((plan) => (
                  <Button
                    key={plan.value}
                    size="sm"
                    variant={(app.preferredNetflixPlan || "standard") === plan.value ? "default" : "outline"}
                    disabled={updatingIndex}
                    onClick={() => handleIndexChange(1, plan.value)}
                  >
                    {plan.label}
                  </Button>
                ))}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Subscriptions</CardTitle>
            <Button
              onClick={handleSyncSubscriptions}
              disabled={syncing}
              variant="outline"
              size="sm"
            >
              {syncing ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Syncing App Data...
                </>
              ) : (
                <>
                  <RefreshCw className="mr-2 h-4 w-4" />
                  Sync App Data
                </>
              )}
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {app.subscriptions?.length === 0 ? (
            <div className="text-center py-8">
              <p className="text-muted-foreground mb-4">
                No subscriptions found. Click &quot;Sync App Data&quot; to import current app metadata and in-app purchases.
              </p>
            </div>
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

      {/* Danger Zone */}
      <Card className="border-destructive/50">
        <CardHeader>
          <CardTitle className="text-destructive">Danger Zone</CardTitle>
          <CardDescription>
            Permanently delete this app and all its data
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button
            variant="destructive"
            onClick={handleDelete}
            disabled={deleting}
          >
            {deleting ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Deleting...
              </>
            ) : (
              <>
                <Trash2 className="mr-2 h-4 w-4" />
                Delete App
              </>
            )}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
