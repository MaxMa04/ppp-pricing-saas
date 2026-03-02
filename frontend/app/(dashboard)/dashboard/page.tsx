"use client";

import { useEffect, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { toast } from "sonner";
import {
  AppWindow,
  Link2,
  Calculator,
  TrendingUp,
  Trash2,
  CheckCircle,
  XCircle,
  Upload,
  FileKey,
  ChevronDown,
  ChevronUp,
  Loader2,
} from "lucide-react";

export default function DashboardPage() {
  const [stats, setStats] = useState({
    connections: 0,
    apps: 0,
    subscriptions: 0,
    regions: 0,
  });
  const [loading, setLoading] = useState(true);

  // Connections state
  const [connections, setConnections] = useState<any[]>([]);
  const [connecting, setConnecting] = useState(false);

  // App Store form
  const [keyId, setKeyId] = useState("");
  const [issuerId, setIssuerId] = useState("");
  const [privateKey, setPrivateKey] = useState("");
  const [keyFileName, setKeyFileName] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);

  // Google Play batch import
  const [showBatchImport, setShowBatchImport] = useState(false);
  const [batchPackageNames, setBatchPackageNames] = useState("");
  const [batchImporting, setBatchImporting] = useState(false);

  const searchParams = useSearchParams();
  const router = useRouter();
  const api = createApi(getCurrentUserToken);

  const fetchData = async () => {
    try {
      const [connectionsData, apps] = await Promise.all([
        api.connections.list(),
        api.apps.list(),
      ]);

      setConnections(connectionsData);

      const subscriptions = apps.reduce(
        (acc: number, app: any) => acc + (app.subscriptionCount || 0),
        0
      );

      setStats({
        connections: connectionsData.length,
        apps: apps.length,
        subscriptions,
        regions: 175,
      });
    } catch (error) {
      console.error("Failed to fetch data:", error instanceof Error ? error.message : "Unknown error");
    } finally {
      setLoading(false);
    }
  };

  // Handle Google OAuth callback result
  useEffect(() => {
    const googleResult = searchParams.get("google");
    if (googleResult === "success") {
      toast.success("Google Play connected successfully!");
      router.replace("/dashboard/connections/import-apps?store=googleplay");
    } else if (googleResult === "error") {
      const message = searchParams.get("message") || "Failed to connect Google Play";
      toast.error(message);
      router.replace("/dashboard", { scroll: false });
    }
  }, [searchParams, router]);

  useEffect(() => {
    fetchData();
  }, []);

  const handleGoogleConnect = async () => {
    setConnecting(true);
    try {
      const { url } = await api.googlePlay.getAuthUrl();
      setConnecting(false);
      window.location.href = url;
    } catch (error: any) {
      toast.error(error.message || "Failed to start Google OAuth");
      setConnecting(false);
    }
  };

  const handleFileRead = (file: File) => {
    if (!file.name.endsWith(".p8")) {
      toast.error("Please upload a .p8 file");
      return;
    }
    const reader = new FileReader();
    reader.onload = (e) => {
      const content = e.target?.result as string;
      setPrivateKey(content);
      setKeyFileName(file.name);
    };
    reader.onerror = () => {
      toast.error("Failed to read file");
    };
    reader.readAsText(file);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files[0];
    if (file) handleFileRead(file);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  };

  const handleAppStoreConnect = async (e: React.FormEvent) => {
    e.preventDefault();
    setConnecting(true);
    try {
      await api.appStore.connect(keyId, issuerId, privateKey);
      toast.success("App Store connected successfully");
      setConnecting(false);
      router.push("/dashboard/connections/import-apps?store=appstore");
    } catch (error: any) {
      toast.error(error.message || "Failed to connect App Store");
      setConnecting(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to remove this connection?")) return;
    try {
      await api.connections.delete(id);
      toast.success("Connection removed");
      fetchData();
    } catch (error: any) {
      toast.error(error.message || "Failed to remove connection");
    }
  };

  const handleBatchImport = async () => {
    const packageNames = batchPackageNames
      .split("\n")
      .map((name) => name.trim())
      .filter((name) => name.length > 0);

    if (packageNames.length === 0) {
      toast.error("Please enter at least one package name");
      return;
    }

    setBatchImporting(true);
    try {
      const result = await api.googlePlay.importApps(packageNames);

      if (result.errors && result.errors.length > 0) {
        const errorMessages = result.errors.map((e: any) => `${e.packageName}: ${e.error}`).join("\n");
        toast.warning(`Imported ${result.imported} apps. Some failed:\n${errorMessages}`);
      } else {
        toast.success(`Successfully imported ${result.imported} app(s)`);
      }

      setBatchPackageNames("");
      setShowBatchImport(false);
      fetchData();

      if (result.imported > 0) {
        router.push("/dashboard/apps");
      }
    } catch (error: any) {
      toast.error(error.message || "Failed to import apps");
    } finally {
      setBatchImporting(false);
    }
  };

  const googleConnection = connections.find((c) => c.storeType === 0);
  const appStoreConnection = connections.find((c) => c.storeType === 1);

  const statCards = [
    {
      title: "Connected Stores",
      value: stats.connections,
      icon: Link2,
      description: "App Store & Google Play",
    },
    {
      title: "Apps",
      value: stats.apps,
      icon: AppWindow,
      description: "Tracked applications",
    },
    {
      title: "Subscriptions",
      value: stats.subscriptions,
      icon: TrendingUp,
      description: "Active subscriptions",
    },
    {
      title: "Regions",
      value: stats.regions,
      icon: Calculator,
      description: "Supported markets",
    },
  ];

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold">Dashboard</h1>
        <p className="text-muted-foreground">
          Overview of your PPP pricing setup
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {statCards.map((stat) => (
          <Card key={stat.title}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">
                {stat.title}
              </CardTitle>
              <stat.icon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {loading ? "-" : stat.value}
              </div>
              <p className="text-xs text-muted-foreground">{stat.description}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        {/* Google Play */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <svg className="h-6 w-6" viewBox="0 0 24 24" fill="currentColor">
                <path d="M3.609 1.814L13.792 12 3.61 22.186a.996.996 0 0 1-.61-.92V2.734a1 1 0 0 1 .609-.92zm10.89 10.893l2.302 2.302-10.937 6.333 8.635-8.635zm3.199-3.198l2.807 1.626a1 1 0 0 1 0 1.73l-2.808 1.626L15.206 12l2.492-2.491zM5.864 2.658L16.8 8.99l-2.302 2.302-8.634-8.634z"/>
              </svg>
              Google Play
            </CardTitle>
            <CardDescription>
              Connect via OAuth to access your Play Console
            </CardDescription>
          </CardHeader>
          <CardContent>
            {googleConnection ? (
              <div className="space-y-4">
                <div className="flex items-center gap-2 text-sm text-green-600">
                  <CheckCircle className="h-4 w-4" />
                  Connected
                </div>
                <div className="text-sm text-muted-foreground">
                  {googleConnection.appCount} apps synced
                </div>

                {/* Batch Import Section */}
                <div className="border-t pt-4">
                  <button
                    type="button"
                    onClick={() => setShowBatchImport(!showBatchImport)}
                    className="flex w-full items-center justify-between text-sm font-medium hover:text-primary"
                  >
                    Import Apps
                    {showBatchImport ? (
                      <ChevronUp className="h-4 w-4" />
                    ) : (
                      <ChevronDown className="h-4 w-4" />
                    )}
                  </button>

                  {showBatchImport && (
                    <div className="mt-3 space-y-3">
                      <p className="text-xs text-muted-foreground">
                        Google Play API requires package names. Enter one per line.
                      </p>
                      <textarea
                        value={batchPackageNames}
                        onChange={(e) => setBatchPackageNames(e.target.value)}
                        placeholder={"com.example.app1\ncom.example.app2\ncom.example.app3"}
                        rows={4}
                        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      />
                      <Button
                        onClick={handleBatchImport}
                        disabled={batchImporting || !batchPackageNames.trim()}
                        size="sm"
                        className="w-full"
                      >
                        {batchImporting ? (
                          <>
                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                            Importing...
                          </>
                        ) : (
                          "Import Apps"
                        )}
                      </Button>
                    </div>
                  )}
                </div>

                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => handleDelete(googleConnection.id)}
                >
                  <Trash2 className="mr-2 h-4 w-4" />
                  Disconnect
                </Button>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <XCircle className="h-4 w-4" />
                  Not connected
                </div>
                <Button onClick={handleGoogleConnect} disabled={connecting}>
                  {connecting ? "Connecting..." : "Connect Google Play"}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>

        {/* App Store */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <svg className="h-6 w-6" viewBox="0 0 24 24" fill="currentColor">
                <path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.81-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M13 3.5c.73-.83 1.94-1.46 2.94-1.5.13 1.17-.34 2.35-1.04 3.19-.69.85-1.83 1.51-2.95 1.42-.15-1.15.41-2.35 1.05-3.11z"/>
              </svg>
              App Store Connect
            </CardTitle>
            <CardDescription>
              Provide your API Key credentials
            </CardDescription>
          </CardHeader>
          <CardContent>
            {appStoreConnection ? (
              <div className="space-y-4">
                <div className="flex items-center gap-2 text-sm text-green-600">
                  <CheckCircle className="h-4 w-4" />
                  Connected
                </div>
                <div className="text-sm text-muted-foreground">
                  {appStoreConnection.appCount} apps synced
                </div>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => handleDelete(appStoreConnection.id)}
                >
                  <Trash2 className="mr-2 h-4 w-4" />
                  Disconnect
                </Button>
              </div>
            ) : (
              <form onSubmit={handleAppStoreConnect} className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="keyId">Key ID</Label>
                  <Input
                    id="keyId"
                    placeholder="e.g., 2X9R4HXF34"
                    value={keyId}
                    onChange={(e) => setKeyId(e.target.value)}
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="issuerId">Issuer ID</Label>
                  <Input
                    id="issuerId"
                    placeholder="UUID from App Store Connect"
                    value={issuerId}
                    onChange={(e) => setIssuerId(e.target.value)}
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label>Private Key (.p8 file)</Label>
                  <div
                    onDrop={handleDrop}
                    onDragOver={handleDragOver}
                    onDragLeave={handleDragLeave}
                    onClick={() => document.getElementById("p8-file-input")?.click()}
                    className={`
                      flex min-h-[120px] w-full cursor-pointer flex-col items-center justify-center gap-2
                      rounded-md border-2 border-dashed transition-colors
                      ${isDragging
                        ? "border-primary bg-primary/5"
                        : keyFileName
                          ? "border-green-500 bg-green-500/5"
                          : "border-input hover:border-primary/50 hover:bg-muted/50"
                      }
                    `}
                  >
                    {keyFileName ? (
                      <>
                        <FileKey className="h-8 w-8 text-green-500" />
                        <span className="text-sm font-medium text-green-600">{keyFileName}</span>
                        <span className="text-xs text-muted-foreground">Click or drop to replace</span>
                      </>
                    ) : (
                      <>
                        <Upload className={`h-8 w-8 ${isDragging ? "text-primary" : "text-muted-foreground"}`} />
                        <span className="text-sm text-muted-foreground">
                          {isDragging ? "Drop your .p8 file here" : "Drag & drop your .p8 file here"}
                        </span>
                        <span className="text-xs text-muted-foreground">or click to browse</span>
                      </>
                    )}
                  </div>
                  <input
                    id="p8-file-input"
                    type="file"
                    accept=".p8"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) handleFileRead(file);
                    }}
                  />
                </div>
                <Button type="submit" disabled={connecting || !privateKey}>
                  {connecting ? "Connecting..." : "Connect App Store"}
                </Button>
              </form>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Getting Started</CardTitle>
        </CardHeader>
        <CardContent>
          <ol className="list-inside list-decimal space-y-3 text-sm">
            <li className={stats.connections > 0 ? "text-muted-foreground line-through" : ""}>
              Connect your App Store or Google Play account
            </li>
            <li className={stats.apps > 0 ? "text-muted-foreground line-through" : ""}>
              Import your apps and subscriptions
            </li>
            <li>Review suggested PPP prices for each region</li>
            <li>Apply pricing changes with one click</li>
            <li>Monitor your revenue across markets</li>
          </ol>
        </CardContent>
      </Card>
    </div>
  );
}
