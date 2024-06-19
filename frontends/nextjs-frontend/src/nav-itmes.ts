import { Home, LineChart, Package, ShoppingCart, Users } from "lucide-react"

type NavItem = {
  url: string
  icon?: any
  label: string
  badge?: number
  group? : string
  children?: ChildNavItem[]
}

type ChildNavItem = Omit<NavItem, 'group'>

export const navItmes: NavItem[] = [
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
