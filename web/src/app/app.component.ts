import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { IdleTimeoutService } from './core/services/idle-timeout.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  title = 'vitalq-web';

  /** Injected workstation idle watchdog */
  private idleTimeout = inject(IdleTimeoutService);

  ngOnInit(): void {
    // Activate 15-minute clinical workstation auto-lock (HIPAA Section 164.312)
    this.idleTimeout.startWatching();
  }
}
