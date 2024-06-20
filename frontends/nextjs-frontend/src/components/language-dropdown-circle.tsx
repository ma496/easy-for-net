import { useLanguage } from "@/hooks/use-language";
import { Button } from "./ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "./ui/dropdown-menu";
import { languages } from "@/config";
import { useLocale } from "next-intl";

export function LanguageDropdownCircle() {
  const locale = useLocale()
  const { changeLanguage, getLanguage } = useLanguage()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="secondary" size="icon" className="rounded-full">
          <img
            src={`/assets/images/flags/${getLanguage(locale)?.flag.toUpperCase()}.svg`}
            alt="image"
            className="h-5 w-5 rounded-full object-cover" />
          <span className="sr-only">Select language</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {
          languages.map(l => (
            <DropdownMenuItem
              key={l.code}
              onClick={() => changeLanguage(l.code)}
              className={`${l.code === locale ? 'bg-muted' : ''}`}
            >
              <div className="flex gap-2 items-center">
                <img
                  src={`/assets/images/flags/${getLanguage(l.code)?.flag.toUpperCase()}.svg`}
                  alt="image"
                  className="h-4 w-4 rounded-full object-cover" />
                <span>{l.name}</span>
              </div>
            </DropdownMenuItem>
          ))
        }
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
