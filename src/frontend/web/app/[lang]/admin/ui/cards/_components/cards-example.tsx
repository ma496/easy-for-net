import { Button, Card, CardContent, CardFooter, CardHeader, CardTitle, CodeShowcase } from '@/components/ui'

/**
 * Interactive showcase component that demonstrates basic, full-width, custom-sized, custom-spacing, product, and variant Card configurations alongside their source snippets.
 */
export const CardsExample = () => {
  const basicCardCode = `<Card>
  <CardHeader>
    <CardTitle>Card Title</CardTitle>
  </CardHeader>
  <CardContent>
    <p className="text-white-dark">Your content here</p>
  </CardContent>
</Card>`

  const fullWidthCardCode = `<Card className="w-full">
  <CardHeader>
    <CardTitle>Wide Card</CardTitle>
  </CardHeader>
  <CardContent>
    <p className="text-white-dark">Full width content</p>
  </CardContent>
</Card>`

  const customSizedCardCode = `<Card className="w-75">
  <CardHeader>
    <CardTitle>Custom Width</CardTitle>
  </CardHeader>
  <CardContent>
    <p className="text-white-dark">Content with custom width</p>
  </CardContent>
</Card>`

  const customSpacingCardCode = `<Card className="max-w-md">
  <CardHeader className="p-8">
    <CardTitle className="text-2xl">Custom Spacing</CardTitle>
  </CardHeader>
  <CardContent className="p-8">
    <p className="text-white-dark">Content with custom padding</p>
  </CardContent>
</Card>`

  const productCardCode = `<Card>
  <CardHeader>
    <CardTitle>Product Card</CardTitle>
    <p className="text-sm text-white-dark">Premium Package</p>
  </CardHeader>
  <CardContent>
    <p>Complete access to all premium features</p>
    <ul className="mt-2 space-y-1">
      <li>✓ Advanced Analytics</li>
      <li>✓ Priority Support</li>
      <li>✓ Custom Integrations</li>
      <li>✓ Unlimited Storage</li>
    </ul>
  </CardContent>
  <CardFooter className="gap-4 justify-between">
    <p className="font-semibold text-lg">$99/month</p>
    <Button size="sm">Subscribe Now</Button>
  </CardFooter>
</Card>`

  const variantCardsCode = `{/* Light Card */}
<Card className="bg-white border border-gray-200">
  <CardContent className="p-6">
    <h3 className="font-semibold text-gray-900">Light Card</h3>
    <p className="text-gray-600 mt-2">Clean and minimal design</p>
  </CardContent>
</Card>

{/* Primary Card */}
<Card className="bg-primary text-white">
  <CardContent className="p-6">
    <h3 className="font-semibold">Primary Card</h3>
    <p className="opacity-90 mt-2">Highlighted content card</p>
  </CardContent>
</Card>

{/* Success Card */}
<Card className="bg-success text-white">
  <CardContent className="p-6">
    <h3 className="font-semibold">Success Card</h3>
    <p className="opacity-90 mt-2">Positive feedback design</p>
  </CardContent>
</Card>`

  return (
    <div className="container mx-auto space-y-8 p-6">
      <div className="mb-8">
        <h1 className="mb-2 text-3xl font-bold">Card Components</h1>
        <p className="text-white-dark">Flexible and extensible content containers with multiple variants and customization options.</p>
      </div>

      <div className="grid gap-8">
        {/* Basic Card */}
        <CodeShowcase
          title="Basic Card"
          description="Simple card with header and content"
          code={basicCardCode}
          preview={
            <Card className="w-full max-w-md">
              <CardHeader>
                <CardTitle>Card Title</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-white-dark">Your content here</p>
              </CardContent>
            </Card>
          }
        />

        {/* Full Width Card */}
        <CodeShowcase
          title="Full Width Card"
          description="Card that spans the full container width"
          code={fullWidthCardCode}
          preview={
            <Card className="w-full">
              <CardHeader>
                <CardTitle>Wide Card</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-white-dark">Full width content</p>
              </CardContent>
            </Card>
          }
        />

        {/* Custom Sized Card */}
        <CodeShowcase
          title="Custom Width Card"
          description="Card with fixed width dimensions"
          code={customSizedCardCode}
          preview={
            <Card className="w-75">
              <CardHeader>
                <CardTitle>Custom Width</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-white-dark">Content with custom width</p>
              </CardContent>
            </Card>
          }
        />

        {/* Custom Spacing Card */}
        <CodeShowcase
          title="Custom Spacing"
          description="Card with custom padding and spacing"
          code={customSpacingCardCode}
          preview={
            <Card className="max-w-md">
              <CardHeader className="p-8">
                <CardTitle className="text-2xl">Custom Spacing</CardTitle>
              </CardHeader>
              <CardContent className="p-8">
                <p className="text-white-dark">Content with custom padding</p>
              </CardContent>
            </Card>
          }
        />

        {/* Product Card */}
        <CodeShowcase
          title="Product Card"
          description="Complete card with header, content, and footer sections"
          code={productCardCode}
          preview={
            <Card className="max-w-md">
              <CardHeader>
                <CardTitle>Product Card</CardTitle>
                <p className="text-sm text-white-dark">Premium Package</p>
              </CardHeader>
              <CardContent>
                <p>Complete access to all premium features</p>
                <ul className="mt-2 space-y-1">
                  <li>✓ Advanced Analytics</li>
                  <li>✓ Priority Support</li>
                  <li>✓ Custom Integrations</li>
                  <li>✓ Unlimited Storage</li>
                </ul>
              </CardContent>
              <CardFooter className="justify-between gap-4">
                <p className="text-lg font-semibold">$99/month</p>
                <Button size="sm">Subscribe Now</Button>
              </CardFooter>
            </Card>
          }
        />

        {/* Card Variants */}
        <CodeShowcase
          title="Card Variants"
          description="Different card styles and color schemes"
          code={variantCardsCode}
          preview={
            <div className="grid w-full grid-cols-1 gap-4 md:grid-cols-3">
              {/* Light Card */}
              <Card className="border border-gray-200 bg-white dark:bg-gray-50">
                <CardContent className="p-6">
                  <h3 className="font-semibold text-gray-900">Light Card</h3>
                  <p className="mt-2 text-gray-600">Clean and minimal design</p>
                </CardContent>
              </Card>

              {/* Primary Card */}
              <Card className="bg-primary text-white">
                <CardContent className="p-6">
                  <h3 className="font-semibold">Primary Card</h3>
                  <p className="mt-2 opacity-90">Highlighted content card</p>
                </CardContent>
              </Card>

              {/* Success Card */}
              <Card className="bg-success text-white">
                <CardContent className="p-6">
                  <h3 className="font-semibold">Success Card</h3>
                  <p className="mt-2 opacity-90">Positive feedback design</p>
                </CardContent>
              </Card>
            </div>
          }
        />
      </div>
    </div>
  )
}
