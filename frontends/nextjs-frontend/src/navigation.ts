import { Home, LineChart, Package, ShoppingCart, Users } from "lucide-react"

type Navigation = {
  url: string
  icon?: any
  label: string
  badge?: number
  group? : string
  children?: ChildNavigation[]
}

type ChildNavigation = Omit<Navigation, 'group'>

export const navigations: Navigation[] = [
  {
    url: '/',
    icon: Home,
    label: 'Dashboard'
  },
  {
    url: '/orders',
    icon: ShoppingCart,
    label: 'Orders',
    badge: 6
  },
  {
    url: '/products',
    icon: Package,
    label: 'Products'
  },
  {
    url: '/customers',
    icon: Users,
    label: 'Customers'
  },
  {
    url: '/analytics',
    icon: LineChart,
    label: 'Analytics'
  },
]
