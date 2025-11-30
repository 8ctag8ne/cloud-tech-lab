# Parallel Sieve of Eratosthenes with PARCS on Google Cloud

## Prerequisites

- Google Cloud account with billing enabled
- Google Cloud SDK installed on Windows
- Python 3.x

## Step 1: Install Google Cloud SDK

### Download and Install
1. Download from: https://cloud.google.com/sdk/docs/install#windows
2. Run `GoogleCloudSDKInstaller.exe`
3. Follow installation wizard
4. Restart PowerShell/Command Prompt

### Verify Installation
```powershell
gcloud --version
```

## Step 2: Initial Setup

### Login to Google Cloud
```powershell
gcloud auth login
```

### Create Project
```powershell
gcloud projects create parcs-sieve-project --name="PARCS Sieve"
```

Replace `parcs-sieve-project` with your unique project ID.

### Set Active Project
```powershell
gcloud config set project parcs-sieve-project
```

### Enable Billing
Visit: https://console.cloud.google.com/billing

Link your billing account to the project.

### Enable Compute Engine API
```powershell
gcloud services enable compute.googleapis.com
```

### Set Region and Zone
```powershell
gcloud config set compute/zone europe-central2-a
gcloud config set compute/region europe-central2
```

**Available zones:**
- `europe-central2-a` (Poland) - closest to Ukraine
- `europe-west1-b` (Belgium)
- `europe-north1-a` (Finland)
- `us-central1-a` (Iowa, USA)

## Step 3: Configure Firewall

```powershell
gcloud compute firewall-rules create allow-all --direction=INGRESS --priority=1000 --network=default --action=ALLOW --rules=all --source-ranges=0.0.0.0/0
```

**Note:** This opens all ports. For production, use specific ports only.

## Step 4: Create VM Instances with PARCS

### Create Master Node
```powershell
gcloud compute instances create-with-container master --container-image=registry.hub.docker.com/hummer12007/parcs-node --container-env PARCS_ARGS="master" --zone=europe-central2-a
```

**Output example:**
```
NAME    ZONE                INTERNAL_IP  EXTERNAL_IP
master  europe-central2-a   10.166.0.2   34.118.XX.XX
```

**Important:** Save the `INTERNAL_IP` (e.g., `10.166.0.2`)

### Create Worker Nodes

Replace `10.166.0.2` with your master's INTERNAL_IP:

```powershell
gcloud compute instances create-with-container worker1 worker2 worker3 worker4 worker5 worker6 worker7 --container-image=registry.hub.docker.com/hummer12007/parcs-node --container-env PARCS_ARGS="worker 10.166.0.2" --zone=europe-central2-a
```

## Step 5: Get Master External IP

```powershell
gcloud compute instances list --filter="name=master" --format="get(networkInterfaces[0].accessConfigs[0].natIP)"
```

Example output: `34.118.123.45`

## Step 6: Access PARCS Web Interface

Open in browser:
```
http://EXTERNAL_IP:8080
```

Replace `EXTERNAL_IP` with the IP from Step 5.

## Step 7: Upload and Run Code

### Via Web Interface

1. Open `http://EXTERNAL_IP:8080`
2. Click "Choose File" under "Module"
3. Upload `sieve_eratosthenes.py`
4. Click "Choose File" under "Input"
5. Upload `input.txt` (containing a number, e.g., `10000000`)
6. Select number of workers (1-7)
7. Click "Run"
8. Wait for completion
9. Download `output.txt`

### Input File Format

**input.txt:**
```
10000000
```

Just a single number - the upper limit for finding primes.

### Recommended Input Values

- `1000000` (10^6) - fast, < 1 second
- `10000000` (10^7) - moderate, 4-9 seconds
- `50000000` (5×10^7) - slower, 14-37 seconds
- `100000000` (10^8) - slow, 30-60+ seconds

## Step 8: Managing Instances

### List All Instances
```powershell
gcloud compute instances list
```

### Stop Instances (to save money)
```powershell
gcloud compute instances stop master worker1 worker2 worker3 worker4 worker5 worker6 worker7 --zone=europe-central2-a
```

### Start Instances
```powershell
gcloud compute instances start master worker1 worker2 worker3 worker4 worker5 worker6 worker7 --zone=europe-central2-a
```

### Delete Instances (cleanup)
```powershell
gcloud compute instances delete master worker1 worker2 worker3 worker4 worker5 worker6 worker7 --zone=europe-central2-a
```

### Delete Firewall Rule
```powershell
gcloud compute firewall-rules delete allow-all
```

## Troubleshooting

### Web Interface Not Loading

**Check external IP:**
```powershell
gcloud compute instances list
```

**Wait 1-2 minutes** after creation for Docker containers to start.

**Check if master is running:**
```powershell
gcloud compute ssh master --zone=europe-central2-a --command="sudo docker ps"
```

### Workers Not Connecting

**Verify internal IP is correct:**
```powershell
gcloud compute instances describe master --zone=europe-central2-a --format="get(networkInterfaces[0].networkIP)"
```

If wrong, recreate workers with correct IP.

### Job Hangs or Fails

**Restart all instances:**
```powershell
gcloud compute instances stop master worker1 worker2 worker3 worker4 worker5 worker6 worker7 --zone=europe-central2-a
gcloud compute instances start master worker1 worker2 worker3 worker4 worker5 worker6 worker7 --zone=europe-central2-a
```

**Check logs on master:**
```powershell
gcloud compute ssh master --zone=europe-central2-a
sudo docker logs $(sudo docker ps -q)
```

### Error: "not enough data"

- Input value too large (> 10^9 causes memory issues)
- Restart instances
- Use smaller input value

## Cost Management

### Free Tier
- Google Cloud offers $300 free credits for 90 days
- e2-micro and e2-small instances are free tier eligible

### Estimated Costs (without free credits)
- Master (e2-medium): ~$0.03/hour
- Workers (e2-small): ~$0.02/hour each
- 1 master + 7 workers: ~$0.17/hour

**Always stop or delete instances when not in use!**

### Check Current Costs
```powershell
gcloud billing accounts list
```

Visit: https://console.cloud.google.com/billing

## Quick Commands Reference

```powershell
# Create everything
gcloud compute firewall-rules create allow-all --direction=INGRESS --priority=1000 --network=default --action=ALLOW --rules=all --source-ranges=0.0.0.0/0
gcloud compute instances create-with-container master --container-image=registry.hub.docker.com/hummer12007/parcs-node --container-env PARCS_ARGS="master" --zone=europe-central2-a

# Get master internal IP
gcloud compute instances describe master --zone=europe-central2-a --format="get(networkInterfaces[0].networkIP)"

# Create workers (replace 10.166.0.2 with your internal IP)
gcloud compute instances create-with-container worker1 worker2 worker3 worker4 worker5 worker6 worker7 --container-image=registry.hub.docker.com/hummer12007/parcs-node --container-env PARCS_ARGS="worker 10.166.0.2" --zone=europe-central2-a

# Get external IP
gcloud compute instances list --filter="name=master" --format="get(networkInterfaces[0].accessConfigs[0].natIP)"

# Open http://EXTERNAL_IP:8080

# Cleanup
gcloud compute instances delete master worker1 worker2 worker3 worker4 worker5 worker6 worker7 --zone=europe-central2-a --quiet
gcloud compute firewall-rules delete allow-all --quiet
```

## Project Structure

```
project/
├── sieve_eratosthenes.py   # Main algorithm
├── input.txt               # Input data (single number)
├── output.txt              # Results (generated)
└── README.md               # This file
```

## File: sieve_eratosthenes.py

This is your main algorithm file. Upload it via PARCS web interface.

## File: input.txt

Simple text file with a single number:
```
10000000
```

## Output Format

The `output.txt` will contain comma-separated prime numbers:
```
2, 3, 5, 7, 11, 13, 17, 19, 23, 29, ...
```

## Support

- Google Cloud Documentation: https://cloud.google.com/docs
- PARCS Documentation: https://git.sr.ht/~hummer12007/parcs-python
- Docker Hub: https://hub.docker.com/r/hummer12007/parcs-node

## License

This project is for educational purposes.