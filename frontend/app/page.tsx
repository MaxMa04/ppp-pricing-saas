import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Sparkles,
  Globe,
  Zap,
  BarChart3,
  Link2,
  ChevronRight,
  Check,
  Star,
  HelpCircle,
  ArrowRight,
  Apple,
  Play,
} from "lucide-react";

export default function Home() {
  return (
    <div className="flex min-h-screen flex-col">
      {/* Header */}
      <header className="sticky top-0 z-50 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="container mx-auto flex h-16 items-center justify-between px-4">
          <div className="flex items-center gap-8">
            <Link href="/" className="flex items-center gap-2">
              <div className="h-8 w-8 rounded-lg bg-accent" />
              <span className="text-xl font-bold">PriceParity</span>
            </Link>
            <nav className="hidden md:flex items-center gap-6">
              <Link href="#features" className="text-sm font-medium text-muted-foreground hover:text-foreground transition-colors">
                Features
              </Link>
              <Link href="#pricing" className="text-sm font-medium text-muted-foreground hover:text-foreground transition-colors">
                Pricing
              </Link>
              <Link href="#faq" className="text-sm font-medium text-muted-foreground hover:text-foreground transition-colors">
                FAQ
              </Link>
            </nav>
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

      <main className="flex-1">
        {/* Hero Section */}
        <section className="container mx-auto px-4 py-24 text-center">
          <div className="inline-flex items-center gap-2 rounded-full bg-accent/10 px-4 py-1.5 text-sm text-accent mb-6">
            <Sparkles className="h-4 w-4" />
            Now with Big Mac, Netflix & Working Hours Index
          </div>
          <h1 className="text-4xl font-bold tracking-tight sm:text-5xl md:text-6xl lg:text-7xl">
            Localize Your App Prices
            <br />
            <span className="text-accent">Increase Global Revenue</span>
          </h1>
          <p className="mx-auto mt-6 max-w-2xl text-lg text-muted-foreground">
            Stop leaving money on the table. Automatically adjust your subscription prices based on
            Purchasing Power Parity to maximize revenue in every market while keeping your app affordable.
          </p>
          <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4">
            <Link href="/register">
              <Button size="lg" className="gap-2">
                Start Free Trial
                <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
            <Link href="#how-it-works">
              <Button variant="outline" size="lg">
                See How It Works
              </Button>
            </Link>
          </div>
          <p className="mt-4 text-sm text-muted-foreground">
            No credit card required • Free for 1 app
          </p>
        </section>

        {/* Trust Bar */}
        <section className="border-y bg-muted/30 py-8">
          <div className="container mx-auto px-4">
            <p className="text-center text-sm text-muted-foreground mb-6">
              Works with the platforms you already use
            </p>
            <div className="flex items-center justify-center gap-12">
              <div className="flex items-center gap-3 text-muted-foreground">
                <Apple className="h-8 w-8" />
                <span className="font-medium">App Store Connect</span>
              </div>
              <div className="flex items-center gap-3 text-muted-foreground">
                <Play className="h-8 w-8" />
                <span className="font-medium">Google Play Console</span>
              </div>
            </div>
          </div>
        </section>

        {/* Features Section */}
        <section id="features" className="py-24">
          <div className="container mx-auto px-4">
            <div className="text-center mb-16">
              <h2 className="text-3xl font-bold sm:text-4xl">
                Everything you need to optimize global pricing
              </h2>
              <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto">
                Powerful tools to help you price your app correctly in every market around the world.
              </p>
            </div>
            <div className="grid gap-8 md:grid-cols-2 lg:grid-cols-4">
              <Card className="border-2 hover:border-accent/50 transition-colors">
                <CardHeader>
                  <div className="mb-2 flex h-12 w-12 items-center justify-center rounded-lg bg-accent/10 text-accent">
                    <Globe className="h-6 w-6" />
                  </div>
                  <CardTitle>Multiple PPP Indexes</CardTitle>
                </CardHeader>
                <CardContent>
                  <CardDescription>
                    Choose from Big Mac Index, Netflix Index, or Working Hours Index to find the perfect pricing strategy.
                  </CardDescription>
                </CardContent>
              </Card>

              <Card className="border-2 hover:border-accent/50 transition-colors">
                <CardHeader>
                  <div className="mb-2 flex h-12 w-12 items-center justify-center rounded-lg bg-accent/10 text-accent">
                    <Link2 className="h-6 w-6" />
                  </div>
                  <CardTitle>Store Integration</CardTitle>
                </CardHeader>
                <CardContent>
                  <CardDescription>
                    Direct integration with App Store Connect and Google Play Console. Sync your apps in seconds.
                  </CardDescription>
                </CardContent>
              </Card>

              <Card className="border-2 hover:border-accent/50 transition-colors">
                <CardHeader>
                  <div className="mb-2 flex h-12 w-12 items-center justify-center rounded-lg bg-accent/10 text-accent">
                    <Zap className="h-6 w-6" />
                  </div>
                  <CardTitle>One-Click Updates</CardTitle>
                </CardHeader>
                <CardContent>
                  <CardDescription>
                    Preview price adjustments and apply them across all 175+ regions with a single click.
                  </CardDescription>
                </CardContent>
              </Card>

              <Card className="border-2 hover:border-accent/50 transition-colors">
                <CardHeader>
                  <div className="mb-2 flex h-12 w-12 items-center justify-center rounded-lg bg-accent/10 text-accent">
                    <BarChart3 className="h-6 w-6" />
                  </div>
                  <CardTitle>Revenue Analytics</CardTitle>
                </CardHeader>
                <CardContent>
                  <CardDescription>
                    Track price changes over time and measure the impact on your global revenue.
                  </CardDescription>
                </CardContent>
              </Card>
            </div>
          </div>
        </section>

        {/* How It Works Section */}
        <section id="how-it-works" className="border-y bg-muted/50 py-24">
          <div className="container mx-auto px-4">
            <div className="text-center mb-16">
              <h2 className="text-3xl font-bold sm:text-4xl">How It Works</h2>
              <p className="mt-4 text-lg text-muted-foreground">
                Get started in minutes, not days
              </p>
            </div>
            <div className="grid gap-8 md:grid-cols-3 max-w-4xl mx-auto">
              <div className="text-center">
                <div className="mb-6 mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-accent text-accent-foreground text-2xl font-bold">
                  1
                </div>
                <h3 className="mb-3 text-xl font-semibold">Connect Your Stores</h3>
                <p className="text-muted-foreground">
                  Link your App Store Connect and Google Play Console accounts with secure OAuth.
                </p>
              </div>
              <div className="text-center relative">
                <div className="hidden md:block absolute top-8 -left-4 w-8 text-muted-foreground">
                  <ChevronRight className="h-8 w-8" />
                </div>
                <div className="mb-6 mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-accent text-accent-foreground text-2xl font-bold">
                  2
                </div>
                <h3 className="mb-3 text-xl font-semibold">Preview Adjustments</h3>
                <p className="text-muted-foreground">
                  See suggested prices for each region based on your chosen PPP index.
                </p>
              </div>
              <div className="text-center relative">
                <div className="hidden md:block absolute top-8 -left-4 w-8 text-muted-foreground">
                  <ChevronRight className="h-8 w-8" />
                </div>
                <div className="mb-6 mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-accent text-accent-foreground text-2xl font-bold">
                  3
                </div>
                <h3 className="mb-3 text-xl font-semibold">Apply & Track</h3>
                <p className="text-muted-foreground">
                  Update prices with one click and track the revenue impact over time.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* Stats Section */}
        <section className="py-24">
          <div className="container mx-auto px-4">
            <div className="grid gap-8 text-center md:grid-cols-4">
              <div>
                <div className="text-5xl font-bold text-accent">175+</div>
                <div className="mt-2 text-muted-foreground">Supported Regions</div>
              </div>
              <div>
                <div className="text-5xl font-bold text-accent">3</div>
                <div className="mt-2 text-muted-foreground">PPP Indexes</div>
              </div>
              <div>
                <div className="text-5xl font-bold text-accent">2x</div>
                <div className="mt-2 text-muted-foreground">Average Revenue Increase</div>
              </div>
              <div>
                <div className="text-5xl font-bold text-accent">1-Click</div>
                <div className="mt-2 text-muted-foreground">Price Updates</div>
              </div>
            </div>
          </div>
        </section>

        {/* Testimonial Section */}
        <section className="border-y bg-muted/30 py-24">
          <div className="container mx-auto px-4 max-w-3xl text-center">
            <div className="flex justify-center gap-1 mb-6">
              {[...Array(5)].map((_, i) => (
                <Star key={i} className="h-6 w-6 fill-yellow-400 text-yellow-400" />
              ))}
            </div>
            <blockquote className="text-2xl font-medium leading-relaxed">
              &ldquo;PriceParity helped us increase our revenue by 40% in emerging markets
              without any extra effort. The Big Mac Index pricing just makes sense for our global audience.&rdquo;
            </blockquote>
            <div className="mt-8 flex items-center justify-center gap-4">
              <div className="h-14 w-14 rounded-full bg-accent flex items-center justify-center text-accent-foreground font-bold text-xl">
                M
              </div>
              <div className="text-left">
                <div className="font-semibold">Max Mustermann</div>
                <div className="text-sm text-muted-foreground">Indie App Developer • 50k+ Downloads</div>
              </div>
            </div>
          </div>
        </section>

        {/* Pricing Section */}
        <section id="pricing" className="py-24">
          <div className="container mx-auto px-4">
            <div className="text-center mb-16">
              <h2 className="text-3xl font-bold sm:text-4xl">Simple, transparent pricing</h2>
              <p className="mt-4 text-lg text-muted-foreground">
                Start free. Upgrade when you need more.
              </p>
            </div>
            <div className="grid gap-8 md:grid-cols-3 max-w-5xl mx-auto">
              {/* Free Tier */}
              <Card className="border-2">
                <CardHeader>
                  <CardTitle>Free</CardTitle>
                  <div className="mt-4">
                    <span className="text-4xl font-bold">$0</span>
                    <span className="text-muted-foreground">/month</span>
                  </div>
                  <CardDescription className="mt-2">Perfect for trying things out</CardDescription>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-3">
                    {["1 App", "All PPP Indexes", "175+ Regions", "Manual Updates"].map((feature) => (
                      <li key={feature} className="flex items-center gap-2">
                        <Check className="h-4 w-4 text-accent" />
                        <span className="text-sm">{feature}</span>
                      </li>
                    ))}
                  </ul>
                  <Link href="/register" className="block mt-6">
                    <Button variant="outline" className="w-full">Get Started</Button>
                  </Link>
                </CardContent>
              </Card>

              {/* Pro Tier */}
              <Card className="border-2 border-accent relative">
                <div className="absolute -top-3 left-1/2 -translate-x-1/2">
                  <span className="bg-accent text-accent-foreground text-xs font-medium px-3 py-1 rounded-full">
                    Most Popular
                  </span>
                </div>
                <CardHeader>
                  <CardTitle>Pro</CardTitle>
                  <div className="mt-4">
                    <span className="text-4xl font-bold">$19</span>
                    <span className="text-muted-foreground">/month</span>
                  </div>
                  <CardDescription className="mt-2">For serious app developers</CardDescription>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-3">
                    {["Unlimited Apps", "All PPP Indexes", "175+ Regions", "One-Click Updates", "Price History", "Priority Support"].map((feature) => (
                      <li key={feature} className="flex items-center gap-2">
                        <Check className="h-4 w-4 text-accent" />
                        <span className="text-sm">{feature}</span>
                      </li>
                    ))}
                  </ul>
                  <Link href="/register" className="block mt-6">
                    <Button className="w-full">Start Free Trial</Button>
                  </Link>
                </CardContent>
              </Card>

              {/* Enterprise Tier */}
              <Card className="border-2">
                <CardHeader>
                  <CardTitle>Enterprise</CardTitle>
                  <div className="mt-4">
                    <span className="text-4xl font-bold">Custom</span>
                  </div>
                  <CardDescription className="mt-2">For large teams and agencies</CardDescription>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-3">
                    {["Everything in Pro", "Custom Integrations", "Dedicated Support", "SLA Guarantee", "Team Management"].map((feature) => (
                      <li key={feature} className="flex items-center gap-2">
                        <Check className="h-4 w-4 text-accent" />
                        <span className="text-sm">{feature}</span>
                      </li>
                    ))}
                  </ul>
                  <Link href="mailto:hello@priceparity.app" className="block mt-6">
                    <Button variant="outline" className="w-full">Contact Sales</Button>
                  </Link>
                </CardContent>
              </Card>
            </div>
          </div>
        </section>

        {/* FAQ Section */}
        <section id="faq" className="border-y bg-muted/50 py-24">
          <div className="container mx-auto px-4 max-w-3xl">
            <div className="text-center mb-16">
              <h2 className="text-3xl font-bold sm:text-4xl">Frequently Asked Questions</h2>
            </div>
            <div className="space-y-6">
              {[
                {
                  q: "What is Purchasing Power Parity (PPP)?",
                  a: "PPP is an economic theory that compares different currencies through a market basket of goods. We use indexes like the Big Mac Index to determine fair local prices that account for the actual purchasing power in each country."
                },
                {
                  q: "How do you calculate the suggested prices?",
                  a: "We combine PPP data from multiple sources (Big Mac Index, Netflix pricing, working hour costs) with current exchange rates to suggest prices that are fair for each market while maximizing your revenue."
                },
                {
                  q: "Will this actually increase my revenue?",
                  a: "Most developers see a 20-40% increase in revenue from emerging markets. By making your app more affordable locally, you convert users who would otherwise not purchase."
                },
                {
                  q: "Is my store connection secure?",
                  a: "Yes. We use OAuth 2.0 for Google Play and secure API keys for App Store Connect. Your credentials are encrypted at rest and we never store passwords."
                },
                {
                  q: "Can I preview changes before applying them?",
                  a: "Absolutely. You can preview all suggested price changes for every region before applying them. Nothing is changed in your store until you explicitly approve it."
                }
              ].map((faq, i) => (
                <div key={i} className="rounded-lg border bg-card p-6">
                  <h3 className="flex items-start gap-3 font-semibold">
                    <HelpCircle className="h-5 w-5 text-accent flex-shrink-0 mt-0.5" />
                    {faq.q}
                  </h3>
                  <p className="mt-3 text-muted-foreground pl-8">{faq.a}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* CTA Section */}
        <section className="py-24">
          <div className="container mx-auto px-4 text-center">
            <h2 className="text-3xl font-bold sm:text-4xl">
              Ready to increase your global revenue?
            </h2>
            <p className="mt-4 text-lg text-muted-foreground max-w-xl mx-auto">
              Join developers who are already using PriceParity to optimize their app pricing worldwide.
            </p>
            <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4 max-w-md mx-auto">
              <Input
                type="email"
                placeholder="Enter your email"
                className="h-12"
              />
              <Link href="/register">
                <Button size="lg" className="w-full sm:w-auto whitespace-nowrap">
                  Get Started Free
                </Button>
              </Link>
            </div>
            <p className="mt-4 text-sm text-muted-foreground">
              No credit card required • Setup in under 5 minutes
            </p>
          </div>
        </section>
      </main>

      {/* Footer */}
      <footer className="border-t py-12 bg-muted/30">
        <div className="container mx-auto px-4">
          <div className="grid gap-8 md:grid-cols-4">
            {/* Brand */}
            <div>
              <div className="flex items-center gap-2 mb-4">
                <div className="h-8 w-8 rounded-lg bg-accent" />
                <span className="text-xl font-bold">PriceParity</span>
              </div>
              <p className="text-sm text-muted-foreground">
                Optimize your app&apos;s global pricing with Purchasing Power Parity.
              </p>
            </div>

            {/* Product */}
            <div>
              <h4 className="font-semibold mb-4">Product</h4>
              <ul className="space-y-2 text-sm text-muted-foreground">
                <li><Link href="#features" className="hover:text-foreground transition-colors">Features</Link></li>
                <li><Link href="#pricing" className="hover:text-foreground transition-colors">Pricing</Link></li>
                <li><Link href="#faq" className="hover:text-foreground transition-colors">FAQ</Link></li>
              </ul>
            </div>

            {/* Company */}
            <div>
              <h4 className="font-semibold mb-4">Company</h4>
              <ul className="space-y-2 text-sm text-muted-foreground">
                <li><Link href="/about" className="hover:text-foreground transition-colors">About</Link></li>
                <li><Link href="/blog" className="hover:text-foreground transition-colors">Blog</Link></li>
                <li><Link href="mailto:hello@priceparity.app" className="hover:text-foreground transition-colors">Contact</Link></li>
              </ul>
            </div>

            {/* Legal */}
            <div>
              <h4 className="font-semibold mb-4">Legal</h4>
              <ul className="space-y-2 text-sm text-muted-foreground">
                <li><Link href="/privacy" className="hover:text-foreground transition-colors">Privacy Policy</Link></li>
                <li><Link href="/terms" className="hover:text-foreground transition-colors">Terms of Service</Link></li>
              </ul>
            </div>
          </div>

          <div className="mt-12 pt-8 border-t text-center text-sm text-muted-foreground">
            <p>&copy; {new Date().getFullYear()} PriceParity. All rights reserved.</p>
          </div>
        </div>
      </footer>
    </div>
  );
}
