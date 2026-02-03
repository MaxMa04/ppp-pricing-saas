import Link from "next/link";
import { Button } from "@/components/ui/button";

export default function Home() {
  return (
    <div className="flex min-h-screen flex-col">
      {/* Header */}
      <header className="border-b">
        <div className="container mx-auto flex h-16 items-center justify-between px-4">
          <div className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-lg bg-primary" />
            <span className="text-xl font-bold">PPP Pricing</span>
          </div>
          <nav className="flex items-center gap-4">
            <Link href="/login">
              <Button variant="ghost">Sign in</Button>
            </Link>
            <Link href="/register">
              <Button>Get Started</Button>
            </Link>
          </nav>
        </div>
      </header>

      {/* Hero */}
      <main className="flex-1">
        <section className="container mx-auto px-4 py-24 text-center">
          <h1 className="text-4xl font-bold tracking-tight sm:text-5xl md:text-6xl">
            Optimize Your App&apos;s
            <br />
            <span className="text-primary">Global Pricing</span>
          </h1>
          <p className="mx-auto mt-6 max-w-2xl text-lg text-muted-foreground">
            Automatically adjust your subscription prices based on Purchasing Power Parity.
            Increase revenue by making your app affordable in every market.
          </p>
          <div className="mt-10 flex items-center justify-center gap-4">
            <Link href="/register">
              <Button size="lg">Start Free Trial</Button>
            </Link>
            <Link href="#features">
              <Button variant="outline" size="lg">Learn More</Button>
            </Link>
          </div>
        </section>

        {/* Features */}
        <section id="features" className="border-t bg-muted/50 py-24">
          <div className="container mx-auto px-4">
            <h2 className="text-center text-3xl font-bold">How It Works</h2>
            <div className="mt-12 grid gap-8 md:grid-cols-3">
              <div className="rounded-lg border bg-card p-6">
                <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                  1
                </div>
                <h3 className="mb-2 text-xl font-semibold">Connect Your Stores</h3>
                <p className="text-muted-foreground">
                  Link your App Store Connect and Google Play Console accounts securely.
                </p>
              </div>
              <div className="rounded-lg border bg-card p-6">
                <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                  2
                </div>
                <h3 className="mb-2 text-xl font-semibold">Preview Adjustments</h3>
                <p className="text-muted-foreground">
                  See suggested prices based on Big Mac Index and local purchasing power.
                </p>
              </div>
              <div className="rounded-lg border bg-card p-6">
                <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                  3
                </div>
                <h3 className="mb-2 text-xl font-semibold">Apply with One Click</h3>
                <p className="text-muted-foreground">
                  Update prices across all regions with a single click. Track changes over time.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* Stats */}
        <section className="py-24">
          <div className="container mx-auto px-4">
            <div className="grid gap-8 text-center md:grid-cols-3">
              <div>
                <div className="text-4xl font-bold text-primary">175+</div>
                <div className="mt-2 text-muted-foreground">Supported Regions</div>
              </div>
              <div>
                <div className="text-4xl font-bold text-primary">2x</div>
                <div className="mt-2 text-muted-foreground">Average Revenue Increase</div>
              </div>
              <div>
                <div className="text-4xl font-bold text-primary">100%</div>
                <div className="mt-2 text-muted-foreground">Automated</div>
              </div>
            </div>
          </div>
        </section>
      </main>

      {/* Footer */}
      <footer className="border-t py-8">
        <div className="container mx-auto px-4 text-center text-sm text-muted-foreground">
          <p>&copy; {new Date().getFullYear()} PPP Pricing. All rights reserved.</p>
        </div>
      </footer>
    </div>
  );
}
