"use client";

import { useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { toast } from "sonner";
import { Loader2, Package, Plus, X, ArrowLeft, AlertCircle } from "lucide-react";
import Link from "next/link";

interface AppStoreApp {
  appStoreId: string;
  bundleId: string;
  name: string;
  sku?: string;
}

interface GooglePlayApp {
  packageName: string;
  name: string;
  iconUrl?: string;
  alreadyImported: boolean;
}

export default function ImportAppsPage() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const api = createApi(getCurrentUserToken);

  const store = searchParams.get("store") as "appstore" | "googleplay" | null;

  const [loading, setLoading] = useState(true);
  const [importing, setImporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // App Store state
  const [appStoreApps, setAppStoreApps] = useState<AppStoreApp[]>([]);
  const [selectedAppStoreIds, setSelectedAppStoreIds] = useState<Set<string>>(new Set());

  // Google Play state
  const [googlePlayApps, setGooglePlayApps] = useState<GooglePlayApp[]>([]);
  const [googlePlayNote, setGooglePlayNote] = useState("");
  const [packageNameInput, setPackageNameInput] = useState("");
  const [manualPackageNames, setManualPackageNames] = useState<string[]>([]);

  useEffect(() => {
    if (store === "appstore") {
      fetchAppStoreApps();
    } else if (store === "googleplay") {
      fetchGooglePlayApps();
    } else {
      setLoading(false);
    }
  }, [store]);

  const fetchAppStoreApps = async () => {
    try {
      setError(null);
      const apps = await api.appStore.getAvailableApps();
      setAppStoreApps(apps);
      // Pre-select all apps
      setSelectedAppStoreIds(new Set(apps.map((a) => a.appStoreId)));
    } catch (err: any) {
      const message = err.message || "Failed to fetch apps from App Store";
      setError(message);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  const fetchGooglePlayApps = async () => {
    try {
      setError(null);
      const response = await api.googlePlay.getAvailableApps();
      setGooglePlayApps(response.apps);
      setGooglePlayNote(response.note);
    } catch (err: any) {
      const message = err.message || "Failed to fetch apps from Google Play";
      setError(message);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  const handleAppStoreToggle = (appId: string) => {
    setSelectedAppStoreIds((prev) => {
      const next = new Set(prev);
      if (next.has(appId)) {
        next.delete(appId);
      } else {
        next.add(appId);
      }
      return next;
    });
  };

  const handleSelectAllAppStore = () => {
    if (selectedAppStoreIds.size === appStoreApps.length) {
      setSelectedAppStoreIds(new Set());
    } else {
      setSelectedAppStoreIds(new Set(appStoreApps.map((a) => a.appStoreId)));
    }
  };

  const addPackageName = () => {
    const trimmed = packageNameInput.trim();
    if (!trimmed) return;
    if (manualPackageNames.includes(trimmed)) {
      toast.error("Package name already added");
      return;
    }
    if (!/^[a-z][a-z0-9_]*(\.[a-z0-9_]+)+$/.test(trimmed)) {
      toast.error("Invalid package name format (e.g., com.example.app)");
      return;
    }
    setManualPackageNames((prev) => [...prev, trimmed]);
    setPackageNameInput("");
  };

  const removePackageName = (name: string) => {
    setManualPackageNames((prev) => prev.filter((p) => p !== name));
  };

  const handleImportAppStore = async () => {
    if (selectedAppStoreIds.size === 0) {
      toast.error("Please select at least one app to import");
      return;
    }

    setImporting(true);
    try {
      const result = await api.appStore.importApps(Array.from(selectedAppStoreIds));
      toast.success(`Imported ${result.imported} app(s) successfully`);
      router.push("/dashboard/apps");
    } catch (error: any) {
      toast.error(error.message || "Failed to import apps");
    } finally {
      setImporting(false);
    }
  };

  const handleImportGooglePlay = async () => {
    if (manualPackageNames.length === 0) {
      toast.error("Please add at least one package name to import");
      return;
    }

    setImporting(true);
    try {
      const result = await api.googlePlay.importApps(manualPackageNames);
      if (result.errors && result.errors.length > 0) {
        result.errors.forEach((e) => {
          toast.error(`${e.packageName}: ${e.error}`);
        });
      }
      if (result.imported > 0) {
        toast.success(`Imported ${result.imported} app(s) successfully`);
        router.push("/dashboard/apps");
      }
    } catch (error: any) {
      toast.error(error.message || "Failed to import apps");
    } finally {
      setImporting(false);
    }
  };

  if (!store) {
    return (
      <div className="space-y-8">
        <div>
          <h1 className="text-3xl font-bold">Import Apps</h1>
          <p className="text-muted-foreground">
            Select a store connection to import apps from
          </p>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <Link href="/dashboard/connections/import-apps?store=appstore">
            <Card className="cursor-pointer transition-colors hover:bg-muted/50">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <svg className="h-6 w-6" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.81-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M13 3.5c.73-.83 1.94-1.46 2.94-1.5.13 1.17-.34 2.35-1.04 3.19-.69.85-1.83 1.51-2.95 1.42-.15-1.15.41-2.35 1.05-3.11z"/>
                  </svg>
                  App Store Connect
                </CardTitle>
                <CardDescription>
                  Import apps from your Apple Developer account
                </CardDescription>
              </CardHeader>
            </Card>
          </Link>

          <Link href="/dashboard/connections/import-apps?store=googleplay">
            <Card className="cursor-pointer transition-colors hover:bg-muted/50">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <svg className="h-6 w-6" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M3.609 1.814L13.792 12 3.61 22.186a.996.996 0 0 1-.61-.92V2.734a1 1 0 0 1 .609-.92zm10.89 10.893l2.302 2.302-10.937 6.333 8.635-8.635zm3.199-3.198l2.807 1.626a1 1 0 0 1 0 1.73l-2.808 1.626L15.206 12l2.492-2.491zM5.864 2.658L16.8 8.99l-2.302 2.302-8.634-8.634z"/>
                  </svg>
                  Google Play
                </CardTitle>
                <CardDescription>
                  Import apps from your Google Play Developer account
                </CardDescription>
              </CardHeader>
            </Card>
          </Link>
        </div>

        <div className="flex">
          <Button variant="outline" asChild>
            <Link href="/dashboard">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to Dashboard
            </Link>
          </Button>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // App Store Import UI
  if (store === "appstore") {
    return (
      <div className="space-y-8">
        <div>
          <h1 className="text-3xl font-bold">Import from App Store</h1>
          <p className="text-muted-foreground">
            Select the apps you want to import from your App Store Connect account
          </p>
        </div>

        {error && (
          <Card className="border-destructive">
            <CardContent className="flex items-center gap-4 py-4">
              <AlertCircle className="h-6 w-6 text-destructive" />
              <div className="flex-1">
                <p className="font-medium text-destructive">Failed to load apps</p>
                <p className="text-sm text-muted-foreground">{error}</p>
              </div>
              <Button variant="outline" size="sm" onClick={fetchAppStoreApps}>
                Retry
              </Button>
            </CardContent>
          </Card>
        )}

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>Available Apps</CardTitle>
                <CardDescription>
                  {appStoreApps.length} app(s) found in your account
                </CardDescription>
              </div>
              {appStoreApps.length > 0 && (
                <Button variant="outline" size="sm" onClick={handleSelectAllAppStore}>
                  {selectedAppStoreIds.size === appStoreApps.length ? "Deselect All" : "Select All"}
                </Button>
              )}
            </div>
          </CardHeader>
          <CardContent>
            {!error && appStoreApps.length === 0 ? (
              <p className="text-muted-foreground py-4 text-center">
                No apps found in your App Store Connect account
              </p>
            ) : (
              <div className="space-y-3">
                {appStoreApps.map((app) => (
                  <label
                    key={app.appStoreId}
                    className="flex items-center gap-4 rounded-lg border p-4 cursor-pointer hover:bg-muted/50 transition-colors"
                  >
                    <Checkbox
                      checked={selectedAppStoreIds.has(app.appStoreId)}
                      onCheckedChange={() => handleAppStoreToggle(app.appStoreId)}
                    />
                    <Package className="h-8 w-8 text-muted-foreground" />
                    <div className="flex-1 min-w-0">
                      <p className="font-medium truncate">{app.name}</p>
                      <p className="text-sm text-muted-foreground truncate">
                        {app.bundleId}
                      </p>
                    </div>
                    {app.sku && (
                      <span className="text-xs text-muted-foreground bg-muted px-2 py-1 rounded">
                        {app.sku}
                      </span>
                    )}
                  </label>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <div className="flex gap-4">
          <Button variant="outline" asChild>
            <Link href="/dashboard">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back
            </Link>
          </Button>
          <Button
            onClick={handleImportAppStore}
            disabled={importing || selectedAppStoreIds.size === 0}
          >
            {importing ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Importing...
              </>
            ) : (
              `Import ${selectedAppStoreIds.size} App(s)`
            )}
          </Button>
        </div>
      </div>
    );
  }

  // Google Play Import UI
  if (store === "googleplay") {
    return (
      <div className="space-y-8">
        <div>
          <h1 className="text-3xl font-bold">Import from Google Play</h1>
          <p className="text-muted-foreground">
            Enter the package names of the apps you want to import
          </p>
        </div>

        {error && (
          <Card className="border-destructive">
            <CardContent className="flex items-center gap-4 py-4">
              <AlertCircle className="h-6 w-6 text-destructive" />
              <div className="flex-1">
                <p className="font-medium text-destructive">Connection error</p>
                <p className="text-sm text-muted-foreground">{error}</p>
              </div>
              <Button variant="outline" size="sm" onClick={fetchGooglePlayApps}>
                Retry
              </Button>
            </CardContent>
          </Card>
        )}

        <Card>
          <CardHeader>
            <CardTitle>Add Package Names</CardTitle>
            <CardDescription>
              {googlePlayNote || "Enter your app package names (e.g., com.example.myapp)"}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex gap-2">
              <div className="flex-1">
                <Input
                  placeholder="com.example.myapp"
                  value={packageNameInput}
                  onChange={(e) => setPackageNameInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      addPackageName();
                    }
                  }}
                />
              </div>
              <Button onClick={addPackageName} variant="outline">
                <Plus className="h-4 w-4" />
              </Button>
            </div>

            {manualPackageNames.length > 0 && (
              <div className="space-y-2">
                <Label>Apps to import:</Label>
                <div className="flex flex-wrap gap-2">
                  {manualPackageNames.map((name) => (
                    <span
                      key={name}
                      className="inline-flex items-center gap-1 bg-muted px-3 py-1 rounded-full text-sm"
                    >
                      {name}
                      <button
                        onClick={() => removePackageName(name)}
                        className="text-muted-foreground hover:text-foreground"
                      >
                        <X className="h-3 w-3" />
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            )}

            {googlePlayApps.length > 0 && (
              <div className="pt-4 border-t">
                <Label className="text-muted-foreground">Previously imported apps:</Label>
                <div className="mt-2 space-y-2">
                  {googlePlayApps.map((app) => (
                    <div
                      key={app.packageName}
                      className="flex items-center gap-3 text-sm text-muted-foreground"
                    >
                      <Package className="h-4 w-4" />
                      <span>{app.name}</span>
                      <span className="text-xs">({app.packageName})</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        <div className="flex gap-4">
          <Button variant="outline" asChild>
            <Link href="/dashboard">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back
            </Link>
          </Button>
          <Button
            onClick={handleImportGooglePlay}
            disabled={importing || manualPackageNames.length === 0}
          >
            {importing ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Importing...
              </>
            ) : (
              `Import ${manualPackageNames.length} App(s)`
            )}
          </Button>
        </div>
      </div>
    );
  }

  return null;
}
