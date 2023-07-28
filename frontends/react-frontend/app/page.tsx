import Image from 'next/image'
import {Button} from "@/components/ui/button";

export default function Home() {
  return (
    <div className={'flex justify-center items-center h-screen'}>
      <Button variant={'default'}>Click Me</Button>
    </div>
  )
}
