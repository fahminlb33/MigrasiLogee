# MigrasiLogee

> MIGRAsi is paIN.

This tool is a set of scripts used to help the discovery step and cutover step
in the process of migrating services off from VSAN to Flou Cloud. This tool is
created using .NET 5 so this tool can be run from most OSes.

Available scripts:

- **scale**, used to bulk scale deployment replicas in OpenShift or K3s (up/down pods).
- **dns-propagation**, used to check DNS propagation from Ingress config using `dig`
  internally.
- **uptime**, checks whether a service is accessible from external URL, using a static
  IP or DNS resolver.
- **mongo-size**, get the total database size for all MongoDB instances in OpenShift.
- **mongo-connection**, get the total connection to MongoDB instances in OpenShift.
- **mongo-dump**, dump all MongoDB database instances in OpenShift.
- **kafka-replicas**, reassign all topics in Kafka to 2 partitions and 2 replicas (partition 0 = broker replica 1 and 2; partition 1 = broker replica 2 and 3).

For more information and additional examples, see this [Documentation](https://docs.google.com/document/d/1OkI_4D7qvCb4C3my7KNeFEaZsQRF8XV5PlcDmZ0ZGyg/edit)

## Building

You can build this tool using .NET 5 or newer.

1. Clone this repo.
2. `cd` to this project directory.
3. To run this script directly from source, run `dotnet run -- -h`.
4. To build this script as executable, run `dotnet build -r win-x64 -c Release -o ./build`

You can customize the RID to `win-x64`, `osx-x64`, or `linux-x64`.

For a ready to use executable, you can download the bleeding edge build from `master`
branch from this project's [Github Releases](https://github.com/fahminlb33/MigrasiLogee/releases)
 page.