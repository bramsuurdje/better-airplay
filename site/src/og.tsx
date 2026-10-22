import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { SocialPreview } from '@/components/social-preview'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <SocialPreview />
  </StrictMode>,
)
