import { Directive, ElementRef, OnInit, OnDestroy, inject, Renderer2 } from '@angular/core';

/**
 * ============================================================================
 * DIRECTIVE: TeleportToBodyDirective
 * ============================================================================
 * 
 * PURPOSE:
 * Teleports modal dialogs and overlay backdrops directly to <body>.
 * 
 * WHY THIS IS CRUCIAL:
 * In modern CSS & browser compositors (Chromium/Firefox/Safari), nested scroll
 * containers (overflow-y-auto) and flex layouts isolate stacking and backdrop-filter
 * blur buffers. Teleporting modal overlays directly to document.body ensures:
 * 1. The dark translucent backdrop smoothly covers 100% of the viewport (including
 *    sidebar and topbar header).
 * 2. backdrop-filter: blur(...) blurs the ENTIRE screen with zero clipped / unblurred gaps.
 * 3. Proper clean DOM lifecycle teardown upon modal close or route change.
 * ============================================================================
 */
@Directive({
  selector: '[teleportToBody]',
  standalone: true
})
export class TeleportToBodyDirective implements OnInit, OnDestroy {
  private el = inject(ElementRef);
  private renderer = inject(Renderer2);

  ngOnInit(): void {
    if (typeof document !== 'undefined' && document.body) {
      this.renderer.appendChild(document.body, this.el.nativeElement);
    }
  }

  ngOnDestroy(): void {
    const node = this.el.nativeElement;
    if (node && node.parentNode) {
      this.renderer.removeChild(node.parentNode, node);
    }
  }
}
