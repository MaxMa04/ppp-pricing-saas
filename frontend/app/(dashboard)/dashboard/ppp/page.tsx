"use client";

import { useEffect, useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { Search } from "lucide-react";

export default function PppDataPage() {
  const [multipliers, setMultipliers] = useState<any[]>([]);
  const [filteredMultipliers, setFilteredMultipliers] = useState<any[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchMultipliers = async () => {
      try {
        const api = createApi(getCurrentUserToken);
        const data = await api.ppp.getMultipliers();
        setMultipliers(data);
        setFilteredMultipliers(data);
      } catch (error) {
        console.error("Failed to fetch multipliers:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchMultipliers();
  }, []);

  useEffect(() => {
    const filtered = multipliers.filter(
      (m) =>
        m.regionCode.toLowerCase().includes(search.toLowerCase()) ||
        m.countryName?.toLowerCase().includes(search.toLowerCase())
    );
    setFilteredMultipliers(filtered);
  }, [search, multipliers]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold">PPP Multipliers</h1>
        <p className="text-muted-foreground">
          Purchasing Power Parity data by region
        </p>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>All Regions</CardTitle>
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
          {filteredMultipliers.length === 0 ? (
            <p className="text-center text-muted-foreground py-8">
              {multipliers.length === 0
                ? "No PPP data available. Import data to get started."
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
                    <th className="py-3 text-left font-medium">Updated</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredMultipliers.map((m) => (
                    <tr key={m.regionCode} className="border-b">
                      <td className="py-3 font-mono">{m.regionCode}</td>
                      <td className="py-3">{m.countryName || "-"}</td>
                      <td className="py-3 text-right">
                        <span
                          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                            m.multiplier < 0.5
                              ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                              : m.multiplier < 0.8
                              ? "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200"
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
