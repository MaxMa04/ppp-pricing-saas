"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import {
  ArrowLeft,
  ArrowUpRight,
  ArrowDownRight,
  Minus,
  Loader2,
  PlayCircle,
  Check,
  AlertCircle,
  History,
} from "lucide-react";
import { toast } from "sonner";

interface PricePreview {
  regionCode: string;
  currencyCode: string;
  currentPrice: number | null;
  suggestedPrice: number | null;
  multiplier: number;
  change: number | null;
}

interface PreviewData {
  subscription: { id: string; name: string; productId: string };
  summary: { increases: number; decreases: number; unchanged: number; total: number };
  prices: PricePreview[];
}

export default function SubscriptionDetailPage() {
  const params = useParams();
  const appId = params.id as string;
  const subId = params.subId as string;

  const [subscription, setSubscription] = useState<any>(null);
  const [prices, setPrices] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [preview, setPreview] = useState<PreviewData | null>(null);
  const [showHistory, setShowHistory] = useState(false);
  const [history, setHistory] = useState<any[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(false);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const api = createApi(getCurrentUserToken);
        const [subData, pricesData] = await Promise.all([
          api.subscriptions.get(subId),
          api.subscriptions.getPrices(subId),
        ]);
        setSubscription(subData);
        setPrices(pricesData);
      } catch (error) {
        console.error("Failed to fetch subscription:", error);
        toast.error("Failed to load subscription");
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, [subId]);

  const handlePreview = async () => {
    setPreviewing(true);
    try {
      const api = createApi(getCurrentUserToken);
      const data = await api.subscriptions.previewPrices(subId);
      setPreview(data);
    } catch (error) {
      console.error("Failed to preview prices:", error);
      toast.error("Failed to generate price preview");
    } finally {
      setPreviewing(false);
    }
  };

  const handleApply = async () => {
    if (!confirm("Are you sure you want to apply these price changes to the store? This action will update your subscription prices.")) {
      return;
    }

    setApplying(true);
    try {
      const api = createApi(getCurrentUserToken);
      const result = await api.subscriptions.applyPrices(subId);

      if (result.success) {
        toast.success(`Applied ${result.appliedCount} price changes successfully`);
        if (result.failedCount > 0) {
          toast.warning(`${result.failedCount} price changes failed`);
        }
        setPreview(null);
        // Refresh prices
        const pricesData = await api.subscriptions.getPrices(subId);
        setPrices(pricesData);
      } else {
        toast.error("Failed to apply price changes");
      }
    } catch (error) {
      console.error("Failed to apply prices:", error);
      toast.error(error instanceof Error ? error.message : "Failed to apply prices");
    } finally {
      setApplying(false);
    }
  };

  const loadHistory = async () => {
    setLoadingHistory(true);
    try {
      const api = createApi(getCurrentUserToken);
      const data = await api.subscriptions.getPriceHistory(subId);
      setHistory(data);
      setShowHistory(true);
    } catch (error) {
      console.error("Failed to load history:", error);
      toast.error("Failed to load price history");
    } finally {
      setLoadingHistory(false);
    }
  };

  const formatPrice = (price: number | null, currency: string) => {
    if (price === null || price === undefined) return "-";
    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: currency || "USD",
      minimumFractionDigits: 2,
    }).format(price);
  };

  const formatChange = (change: number | null) => {
    if (change === null || change === undefined) return null;
    const formatted = Math.abs(change).toFixed(1);
    if (change > 0) {
      return (
        <span className="text-red-500 flex items-center gap-1">
          <ArrowUpRight className="h-3 w-3" />
          +{formatted}%
        </span>
      );
    } else if (change < 0) {
      return (
        <span className="text-green-500 flex items-center gap-1">
          <ArrowDownRight className="h-3 w-3" />
          {formatted}%
        </span>
      );
    }
    return (
      <span className="text-muted-foreground flex items-center gap-1">
        <Minus className="h-3 w-3" />
        0%
      </span>
    );
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  if (!subscription) {
    return (
      <div className="text-center py-12">
        <p className="text-muted-foreground">Subscription not found</p>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div className="flex items-center gap-4">
        <Link href={`/dashboard/apps/${appId}`}>
          <Button variant="ghost" size="icon">
            <ArrowLeft className="h-5 w-5" />
          </Button>
        </Link>
        <div>
          <h1 className="text-3xl font-bold">{subscription.name || subscription.productId}</h1>
          <p className="text-muted-foreground">
            {subscription.productId}
            {subscription.basePlanId && ` / ${subscription.basePlanId}`}
            {subscription.billingPeriod && ` - ${subscription.billingPeriod}`}
          </p>
        </div>
      </div>

      {/* Actions */}
      <Card>
        <CardHeader>
          <CardTitle>PPP Price Adjustment</CardTitle>
          <CardDescription>
            Preview and apply Purchasing Power Parity adjusted prices to this subscription
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex gap-4">
            <Button onClick={handlePreview} disabled={previewing}>
              {previewing ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Calculating...
                </>
              ) : (
                <>
                  <PlayCircle className="mr-2 h-4 w-4" />
                  Preview Changes
                </>
              )}
            </Button>
            <Button
              variant="outline"
              onClick={loadHistory}
              disabled={loadingHistory}
            >
              {loadingHistory ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <History className="mr-2 h-4 w-4" />
              )}
              View History
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Preview Results */}
      {preview && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>Price Preview</CardTitle>
                <CardDescription>
                  Review the suggested price changes before applying
                </CardDescription>
              </div>
              <Button onClick={handleApply} disabled={applying}>
                {applying ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Applying...
                  </>
                ) : (
                  <>
                    <Check className="mr-2 h-4 w-4" />
                    Apply Changes
                  </>
                )}
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            {/* Summary */}
            <div className="grid grid-cols-4 gap-4 mb-6">
              <div className="text-center p-4 bg-muted rounded-lg">
                <div className="text-2xl font-bold">{preview.summary.total}</div>
                <div className="text-sm text-muted-foreground">Total Regions</div>
              </div>
              <div className="text-center p-4 bg-red-500/10 rounded-lg">
                <div className="text-2xl font-bold text-red-500">{preview.summary.increases}</div>
                <div className="text-sm text-muted-foreground">Price Increases</div>
              </div>
              <div className="text-center p-4 bg-green-500/10 rounded-lg">
                <div className="text-2xl font-bold text-green-500">{preview.summary.decreases}</div>
                <div className="text-sm text-muted-foreground">Price Decreases</div>
              </div>
              <div className="text-center p-4 bg-muted rounded-lg">
                <div className="text-2xl font-bold">{preview.summary.unchanged}</div>
                <div className="text-sm text-muted-foreground">Unchanged</div>
              </div>
            </div>

            {/* Price Table */}
            <div className="border rounded-lg overflow-hidden">
              <table className="w-full">
                <thead className="bg-muted">
                  <tr>
                    <th className="text-left p-3 font-medium">Region</th>
                    <th className="text-right p-3 font-medium">Current Price</th>
                    <th className="text-right p-3 font-medium">Suggested Price</th>
                    <th className="text-right p-3 font-medium">Multiplier</th>
                    <th className="text-right p-3 font-medium">Change</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {preview.prices
                    .sort((a, b) => (b.change ?? 0) - (a.change ?? 0))
                    .map((price) => (
                      <tr key={price.regionCode} className="hover:bg-muted/50">
                        <td className="p-3">
                          <span className="font-medium">{price.regionCode}</span>
                          <span className="text-muted-foreground ml-2 text-sm">{price.currencyCode}</span>
                        </td>
                        <td className="text-right p-3">
                          {formatPrice(price.currentPrice, price.currencyCode)}
                        </td>
                        <td className="text-right p-3 font-medium">
                          {formatPrice(price.suggestedPrice, price.currencyCode)}
                        </td>
                        <td className="text-right p-3 text-muted-foreground">
                          {price.multiplier.toFixed(2)}x
                        </td>
                        <td className="text-right p-3">
                          {formatChange(price.change)}
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Current Prices (when no preview) */}
      {!preview && prices.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Current Regional Prices</CardTitle>
            <CardDescription>
              {prices.length} regions configured
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="border rounded-lg overflow-hidden">
              <table className="w-full">
                <thead className="bg-muted">
                  <tr>
                    <th className="text-left p-3 font-medium">Region</th>
                    <th className="text-right p-3 font-medium">Current Price</th>
                    <th className="text-right p-3 font-medium">PPP Suggested</th>
                    <th className="text-right p-3 font-medium">Difference</th>
                    <th className="text-right p-3 font-medium">Last Synced</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {prices.map((price) => (
                    <tr key={price.regionCode} className="hover:bg-muted/50">
                      <td className="p-3">
                        <span className="font-medium">{price.regionCode}</span>
                        <span className="text-muted-foreground ml-2 text-sm">{price.currencyCode}</span>
                      </td>
                      <td className="text-right p-3">
                        {formatPrice(price.currentPrice, price.currencyCode)}
                      </td>
                      <td className="text-right p-3">
                        {formatPrice(price.pppSuggestedPrice, price.currencyCode)}
                      </td>
                      <td className="text-right p-3">
                        {formatChange(price.difference)}
                      </td>
                      <td className="text-right p-3 text-sm text-muted-foreground">
                        {price.lastSyncedAt ? new Date(price.lastSyncedAt).toLocaleDateString() : "-"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}

      {/* History */}
      {showHistory && (
        <Card>
          <CardHeader>
            <CardTitle>Price Change History</CardTitle>
            <CardDescription>Recent price adjustments for this subscription</CardDescription>
          </CardHeader>
          <CardContent>
            {history.length === 0 ? (
              <p className="text-center text-muted-foreground py-8">No price changes recorded yet.</p>
            ) : (
              <div className="border rounded-lg overflow-hidden">
                <table className="w-full">
                  <thead className="bg-muted">
                    <tr>
                      <th className="text-left p-3 font-medium">Date</th>
                      <th className="text-left p-3 font-medium">Region</th>
                      <th className="text-right p-3 font-medium">Old Price</th>
                      <th className="text-right p-3 font-medium">New Price</th>
                      <th className="text-left p-3 font-medium">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {history.map((change) => (
                      <tr key={change.id} className="hover:bg-muted/50">
                        <td className="p-3 text-sm">
                          {new Date(change.createdAt).toLocaleString()}
                        </td>
                        <td className="p-3">
                          <span className="font-medium">{change.regionCode}</span>
                          <span className="text-muted-foreground ml-2 text-sm">{change.currencyCode}</span>
                        </td>
                        <td className="text-right p-3">
                          {formatPrice(change.oldPrice, change.currencyCode)}
                        </td>
                        <td className="text-right p-3 font-medium">
                          {formatPrice(change.newPrice, change.currencyCode)}
                        </td>
                        <td className="p-3">
                          {change.status === 1 ? (
                            <span className="inline-flex items-center gap-1 text-green-500">
                              <Check className="h-4 w-4" />
                              Applied
                            </span>
                          ) : change.status === 2 ? (
                            <span className="inline-flex items-center gap-1 text-red-500" title={change.errorMessage}>
                              <AlertCircle className="h-4 w-4" />
                              Failed
                            </span>
                          ) : (
                            <span className="text-muted-foreground">Pending</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
