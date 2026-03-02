"use client";

import { useEffect, useState, useRef } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { Search, RefreshCw, Loader2 } from "lucide-react";
import { toast } from "sonner";

type IndexType = "BigMac" | "Netflix" | "BigMacWorkingHours";

const INDEX_TABS: { id: IndexType; label: string; description: string }[] = [
  { id: "BigMac", label: "Big Mac Index", description: "Classic PPP indicator based on McDonald's Big Mac prices" },
  { id: "Netflix", label: "Netflix Index", description: "Digital goods PPP based on Netflix subscription prices" },
  { id: "BigMacWorkingHours", label: "Working Hours", description: "Purchasing power measured in work time to buy a Big Mac" },
];

async function autoImportForIndex(api: ReturnType<typeof createApi>, indexType: IndexType) {
  if (indexType === "BigMac") {
    return api.ppp.importBigMac();
  } else if (indexType === "Netflix") {
    return api.ppp.importNetflix();
  } else {
    await api.ppp.importWages();
    return api.ppp.calculateWorkingHours();
  }
}

export default function PppDataPage() {
  const [multipliers, setMultipliers] = useState<any[]>([]);
  const [filteredMultipliers, setFilteredMultipliers] = useState<any[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [activeTab, setActiveTab] = useState<IndexType>("BigMac");
  const autoImportedRef = useRef<Set<IndexType>>(new Set());

  const api = createApi(getCurrentUserToken);

  const fetchMultipliers = async (indexType: IndexType, autoImport = true) => {
    setLoading(true);
    try {
      const data = await api.ppp.getMultipliers(indexType);

      // Auto-import if no data exists and we haven't tried yet for this tab
      if (data.length === 0 && autoImport && !autoImportedRef.current.has(indexType)) {
        autoImportedRef.current.add(indexType);
        try {
          const result = await autoImportForIndex(api, indexType);
          toast.success(`Loaded ${result.imported + result.updated} regions`);
          const freshData = await api.ppp.getMultipliers(indexType);
          setMultipliers(freshData);
          setFilteredMultipliers(freshData);
        } catch (importError) {
          console.error("Auto-import failed:", importError);
          toast.error("Failed to load PPP data automatically");
          setMultipliers([]);
          setFilteredMultipliers([]);
        }
      } else {
        setMultipliers(data);
        setFilteredMultipliers(data);
      }
    } catch (error) {
      console.error("Failed to fetch multipliers:", error instanceof Error ? error.message : "Unknown error");
      toast.error("Failed to load multipliers");
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      const result = await autoImportForIndex(api, activeTab);
      toast.success(`Updated: ${result.imported} new, ${result.updated} updated`);
      await fetchMultipliers(activeTab, false);
    } catch (error) {
      console.error("Refresh failed:", error);
      toast.error(error instanceof Error ? error.message : "Refresh failed");
    } finally {
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchMultipliers(activeTab);
  }, [activeTab]);

  useEffect(() => {
    const filtered = multipliers.filter(
      (m) =>
        m.regionCode.toLowerCase().includes(search.toLowerCase()) ||
        m.countryName?.toLowerCase().includes(search.toLowerCase())
    );
    setFilteredMultipliers(filtered);
  }, [search, multipliers]);

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold">PPP Multipliers</h1>
        <p className="text-muted-foreground">
          Purchasing Power Parity data by region
        </p>
      </div>

      {/* Index Type Tabs */}
      <div className="flex flex-wrap gap-2">
        {INDEX_TABS.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
              activeTab === tab.id
                ? "bg-primary text-primary-foreground"
                : "bg-muted hover:bg-muted/80"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Multipliers Table */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>
                {INDEX_TABS.find((t) => t.id === activeTab)?.label} Data
              </CardTitle>
              <p className="text-sm text-muted-foreground mt-1">
                {INDEX_TABS.find((t) => t.id === activeTab)?.description}
              </p>
            </div>
            <div className="flex items-center gap-3">
              <div className="relative w-64">
                <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  placeholder="Search by region or country..."
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  className="pl-9"
                />
              </div>
              <Button
                variant="ghost"
                size="icon"
                onClick={handleRefresh}
                disabled={refreshing || loading}
                title="Refresh data"
              >
                {refreshing ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <RefreshCw className="h-4 w-4" />
                )}
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex flex-col items-center justify-center py-12 gap-3">
              <Loader2 className="h-8 w-8 animate-spin text-primary" />
              <p className="text-sm text-muted-foreground">Loading PPP data...</p>
            </div>
          ) : filteredMultipliers.length === 0 ? (
            <p className="text-center text-muted-foreground py-8">
              {multipliers.length === 0
                ? "No data available. Data will be loaded automatically when needed."
                : "No results found."}
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b">
                    <th className="py-3 text-left font-medium">Region</th>
                    <th className="py-3 text-left font-medium">Country</th>
                    <th className="py-3 text-right font-medium">Multiplier</th>
                    {activeTab === "Netflix" && <th className="py-3 text-left font-medium">Plan</th>}
                    <th className="py-3 text-left font-medium">Source</th>
                    <th className="py-3 text-left font-medium">Data Date</th>
                    <th className="py-3 text-left font-medium">Updated</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredMultipliers.map((m) => (
                    <tr key={`${m.regionCode}-${m.indexType}-${m.planType || "default"}`} className="border-b">
                      <td className="py-3 font-mono">{m.regionCode}</td>
                      <td className="py-3">{m.countryName || "-"}</td>
                      <td className="py-3 text-right">
                        <span
                          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                            m.multiplier < 0.5
                              ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                              : m.multiplier < 0.8
                              ? "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200"
                              : m.multiplier < 1.2
                              ? "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200"
                              : "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200"
                          }`}
                        >
                          {(m.multiplier * 100).toFixed(0)}%
                        </span>
                      </td>
                      {activeTab === "Netflix" && (
                        <td className="py-3 text-muted-foreground">{m.planType || "standard"}</td>
                      )}
                      <td className="py-3 text-muted-foreground">
                        {m.source || "-"}
                      </td>
                      <td className="py-3 text-muted-foreground">
                        {m.dataDate
                          ? new Date(m.dataDate).toLocaleDateString()
                          : "-"}
                      </td>
                      <td className="py-3 text-muted-foreground">
                        {m.updatedAt
                          ? new Date(m.updatedAt).toLocaleDateString()
                          : "-"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
