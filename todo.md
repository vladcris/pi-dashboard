# Roadmap
 - Phase 2: SQLite storage for historical data (EF Core, retention policies)
 - Phase 3: Charts/graphs with Chart.js or Blazor-ApexCharts
 - Phase 4: Alerts (temp > 80C, memory > 90%, throttling detected)
 - Phase 5: Docker container monitoring



  # Check service status
 sudo systemctl status pi-dashboard

 # View logs
 journalctl -u pi-dashboard -f

# Test the dashboard
 curl http://localhost:5000


  To deploy for the first time:                                                                                                                                                                                             
  # 1. Copy the service file to systemd                                                                                                                                                                   
  sudo cp pi-dashboard.service /etc/systemd/system/

  # 2. Enable it (auto-start on boot)
  sudo systemctl daemon-reload
  sudo systemctl enable pi-dashboard

  # 3. Build and deploy
  ./deploy.sh
