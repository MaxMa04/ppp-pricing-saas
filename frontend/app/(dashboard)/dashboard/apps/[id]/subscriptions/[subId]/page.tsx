"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { toast } from "sonner";
import { ArrowLeft, TrendingDown, TrendingUp, Minus, RefreshCw } from "lucide-react";

export default function SubscriptionPricingPage() {
  const params = useParams();
  const appId = params.id as string;
  const subId = params.subId as string;

  const [subscription, setSubscription] = useState<any>(null);
  const [prices, setPrices] = useState<any[]>([]);
  const [preview, setPreview] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [previewing, setPreviewing] = useState(false);

  const api = createApi(getCurrentUserToken);

  const fetchData = async () => {
    try {
      const [subData, pricesData] = await Promise.all([
        api.subscriptions.get(subId),
        api.subscriptions.getPrices(subId),
      ]);
      setSubscription(subData);
      setPrices(pricesData);
    } catch (error) {
      console.error("Failed to fetch data:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [subId]);

  const handlePreview = async () => {
    setPreviewing(true);
    try {
      const data = await api.subscriptions.previewPrices(subId);
      setPreview(data);
    } catch (error: any) {
      toast.error(error.message || "Failed to preview prices");
    } finally {
      setPreviewing(false);
    }
  };

  const getDifferenceIcon = (diff: number | null) => {
    if (diff === null) return <Minus className="h-4 w-4 text-muted-foreground" />;
    if (diff > 5) return <TrendingUp className="h-4 w-4 text-red-500" />;
    if (diff < -5) return <TrendingDown className="h-4 w-4 text-green-500" />;
    return <Minus className="h-4 w-4 text-muted-foreground" />;
  };

  const getDifferenceColor = (diff: number | null) => {
    if (diff === null) return "text-muted-foreground";
    if (Math.abs(diff) <= 10) return "text-green-600";
    if (Math.abs(diff) <= 30) return "text-yellow-600";
    return "text-red-600";
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
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
          <h1 className="text-3xl font-bold">
            {subscription?.name || subscription?.productId}
          </h1>
          <p className="text-muted-foreground">
            {subscription?.productId}
            {subscription?.basePlanId && ` / ${subscription?.basePlanId}`}
          </p>
        </div>
      </div>

      <div className="flex gap-4">
        <Button onClick={handlePreview} disabled={previewing}>
          <RefreshCw className={`mr-2 h-4 w-4 ${previewing ? "animate-spin" : ""}`} />
          {previewing ? "Calculating..." : "Preview PPP Adjustments"}
        </Button>
      </div>

      {preview && (
        <Card>
          <CardHeader>
            <CardTitle>Preview Summary</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-4 md:grid-cols-3">
              <div className="rounded-lg bg-red-50 dark:bg-red-950 p-4">
                <div className="text-2xl font-bold text-red-600">
                  {preview.summary.increases}
                </div>
                <div className="text-sm text-red-600">Price increases</div>
              </div>
              <div className="rounded-lg bg-green-50 dark:bg-green-950 p-4">
                <div className="text-2xl font-bold text-green-600">
                  {preview.summary.decreases}
                </div>
                <div className="text-sm text-green-600">Price decreases</div>
              </div>
              <div className="rounded-lg bg-muted p-4">
                <div className="text-2xl font-bold">
                  {preview.summary.unchanged}
                </div>
                <div className="text-sm text-muted-foreground">Unchanged</div>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Regional Prices</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="py-3 text-left font-medium">Region</th>
                  <th className="py-3 text-right font-medium">Current Price</th>
                  <th className="py-3 text-right font-medium">PPP Suggested</th>
                  <th className="py-3 text-right font-medium">Difference</th>
                </tr>
              </thead>
              <tbody>
                {(preview?.prices || prices).map((price: any) => (
                  <tr key={price.regionCode} className="border-b">
                    <td className="py-3">{price.regionCode}</td>
                    <td className="py-3 text-right">
                      {price.currentPrice != null
                        ? `${price.currencyCode} ${price.currentPrice.toFixed(2)}`
                        : "-"}
                    </td>
                    <td className="py-3 text-right">
                      {(price.pppSuggestedPrice || price.suggestedPrice) != null
                        ? `${price.currencyCode} ${(price.pppSuggestedPrice || price.suggestedPrice).toFixed(2)}`
                        : "-"}
                    </td>
                    <td className="py-3 text-right">
                      <span
                        className={`flex items-center justify-end gap-1 ${getDifferenceColor(
                          price.difference ?? price.change
                        )}`}
                      >
                        {getDifferenceIcon(price.difference ?? price.change)}
                        {(price.difference ?? price.change) != null
                          ? `${(price.difference ?? price.change) > 0 ? "+" : ""}${(price.difference ?? price.change).toFixed(1)}%`
                          : "-"}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
