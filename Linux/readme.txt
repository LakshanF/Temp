** Steps for setting up CAP trace collection on Linux

** 1) Install a globally accessible .NET Core 

https://aka.ms/dotnet-download

Ensure dotnet is accessible globally
dotnet --info


** 2) Install PerfCollect

Follow the steps on https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/linux-performance-tracing.md

curl -OL http://aka.ms/perfcollect
chmod +x perfcollect
sudo ./perfcollect install


** 3) Copy the contents of the below folder to local directory of the client machine

\\clrcap\public\teams\deploy-linux\2.0\* .


** 4) Modify the scripts (manually) to include a few key details

4.a) Add ingress token

$INGRESS_TOKEN = "x";         # eg: ah2ZQBJK70Gygx25gvn2OA

4.b) Add any tags you want to send with your trace

$TAGS = "";                   # ';' seperated list of tags: eg. "TAG1;TAG2";

4.c) Modify how long you want the script to wait in between collections

$LOOP_WAIT_SECONDS = 28800;   # 8 hours

4.d) Modify the collection time

Recommend to set to something >10 minutes.  Recommendation is 1 hour or 6000 seconds

$TRACE_COLLECTION_DURATION = 1800; # (in seconds) 1800 == 30 minutes

4.e) Specific how many collections you would like

$NUMBER_OF_COLLECTIONS = -1;   # <=0 forever, >0 that number of collections


** 3) Run the collection script elevated

sudo sh
./capcollect


** 4) For the process that you are monitoring set the following env variables

(Note: These are case sensitive)
export COMPlus_PerfMapEnabled=1
export COMPlus_EnableEventLog=1

Set these environment variables for all processes you want to monitor

