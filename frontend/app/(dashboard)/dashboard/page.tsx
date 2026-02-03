"use client";

import { useEffect, useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { createApi } from "@/lib/api/client";
import { getCurrentUserToken } from "@/lib/firebase/auth";
import { AppWindow, Link2, Calculator, TrendingUp } from "lucide-react";

export default function DashboardPage() {
  const [stats, setStats] = useState({
    connections: 0,
    apps: 0,
    subscriptions: 0,
    regions: 0,
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const api = createApi(getCurrentUserToken);
        const [connections, apps] = await Promise.all([
          api.connections.list(),
          api.apps.list(),
        ]);

        const subscriptions = apps.reduce(
          (acc: number, app: any) => acc + (app.subscriptionCount || 0),
          0
        );

        setStats({
          connections: connections.length,
          apps: apps.length,
          subscriptions,
          regions: 175, // All supported regions
        });
      } catch (error) {
        console.error("Failed to fetch stats:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchStats();
  }, []);

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

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Quick Actions</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <a
              href="/dashboard/connections"
              className="block rounded-lg border p-4 transition-colors hover:bg-accent"
            >
              <div className="font-medium">Connect a Store</div>
              <div className="text-sm text-muted-foreground">
                Link your App Store or Google Play account
              </div>
            </a>
            <a
              href="/dashboard/apps"
              className="block rounded-lg border p-4 transition-colors hover:bg-accent"
            >
              <div className="font-medium">View Apps</div>
              <div className="text-sm text-muted-foreground">
                Manage your connected applications
              </div>
            </a>
            <a
              href="/dashboard/ppp"
              className="block rounded-lg border p-4 transition-colors hover:bg-accent"
            >
              <div className="font-medium">PPP Multipliers</div>
              <div className="text-sm text-muted-foreground">
                View and customize regional pricing data
              </div>
            </a>
          </CardContent>
        </Card>

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
    </div>
  );
}
