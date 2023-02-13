# PulsarRebus

### Prerequisites

Clone this repo: https://github.com/david-streamlio/pulsar-aop-demo

Go to the bin directory from the repo in your local file explorer and run:
```
docker compose --project-name persistence --file ../infrastructure/pulsar.yaml up -d
```

Then run the following commands:

```
docker exec -it pulsar /pulsar/bin/pulsar-admin namespaces create -b 1 public/claimgen-rebus
```

```
docker exec -it pulsar /pulsar/bin/pulsar-admin namespaces set-retention -s 100M -t 2d  public/claimgen-rebus
```

### Usage
Open the PulsarRebus.sln in Visual Studio