"use client";

import { useEffect, useState } from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { Search, Download, RefreshCw, Loader2 } from "lucide-react";
import { toast } from "sonner";

type IndexType = "BigMac" | "Netflix" | "BigMacWorkingHours";

const INDEX_TABS: { id: IndexType; label: string; description: string }[] = [
  { id: "BigMac", label: "Big Mac Index", description: "Classic PPP indicator based on McDonald's Big Mac prices" },
  { id: "Netflix", label: "Netflix Index", description: "Digital goods PPP based on Netflix subscription prices" },
  { id: "BigMacWorkingHours", label: "Working Hours", description: "Purchasing power measured in work time to buy a Big Mac" },
];

export default function PppDataPage() {
  const [multipliers, setMultipliers] = useState<any[]>([]);
  const [filteredMultipliers, setFilteredMultipliers] = useState<any[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [importing, setImporting] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<IndexType>("BigMac");

  const fetchMultipliers = async (indexType: IndexType) => {
    setLoading(true);
    try {
      const api = createApi(getCurrentUserToken);
      const data = await api.ppp.getMultipliers(indexType);
      setMultipliers(data);
      setFilteredMultipliers(data);
    } catch (error) {
      console.error("Failed to fetch multipliers:", error instanceof Error ? error.message : "Unknown error");
      toast.error("Failed to load multipliers");
    } finally {
      setLoading(false);
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

  const handleImport = async (type: "bigmac" | "netflix" | "working-hours") => {
    setImporting(type);
    try {
      const api = createApi(getCurrentUserToken);
      let result;

      if (type === "bigmac") {
        result = await api.ppp.importBigMac();
      } else if (type === "netflix") {
        result = await api.ppp.importNetflix();
      } else {
        // For working hours, first import wages then calculate
        await api.ppp.importWages();
        result = await api.ppp.calculateWorkingHours();
      }

      toast.success(`Import successful: ${result.imported} new, ${result.updated} updated`);

      // Refresh the current tab's data
      if (
        (type === "bigmac" && activeTab === "BigMac") ||
        (type === "netflix" && activeTab === "Netflix") ||
        (type === "working-hours" && activeTab === "BigMacWorkingHours")
      ) {
        fetchMultipliers(activeTab);
      }
    } catch (error) {
      console.error("Import failed:", error);
      toast.error(error instanceof Error ? error.message : "Import failed");
    } finally {
      setImporting(null);
    }
  };

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

      {/* Import Section */}
      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Import Data</CardTitle>
          <CardDescription>
            {INDEX_TABS.find((t) => t.id === activeTab)?.description}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-3">
            <Button
              onClick={() => handleImport("bigmac")}
              disabled={importing !== null}
              variant={activeTab === "BigMac" ? "default" : "outline"}
            >
              {importing === "bigmac" ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Download className="mr-2 h-4 w-4" />
              )}
              Import Big Mac Index
            </Button>
            <Button
              onClick={() => handleImport("netflix")}
              disabled={importing !== null}
              variant={activeTab === "Netflix" ? "default" : "outline"}
            >
              {importing === "netflix" ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Download className="mr-2 h-4 w-4" />
              )}
              Import Netflix Index
            </Button>
            <Button
              onClick={() => handleImport("working-hours")}
              disabled={importing !== null}
              variant={activeTab === "BigMacWorkingHours" ? "default" : "outline"}
            >
              {importing === "working-hours" ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Calculate Working Hours
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Multipliers Table */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {INDEX_TABS.find((t) => t.id === activeTab)?.label} Data
            </CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Search by region or country..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="pl-9"
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center justify-center py-12">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
            </div>
          ) : filteredMultipliers.length === 0 ? (
            <p className="text-center text-muted-foreground py-8">
              {multipliers.length === 0
                ? `No ${INDEX_TABS.find((t) => t.id === activeTab)?.label} data available. Click the import button above to get started.`
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
                    <th className="py-3 text-left font-medium">Source</th>
                    <th className="py-3 text-left font-medium">Data Date</th>
                    <th className="py-3 text-left font-medium">Updated</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredMultipliers.map((m) => (
                    <tr key={`${m.regionCode}-${m.indexType}`} className="border-b">
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
