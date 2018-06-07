---
services: storage, cognitive-services
platforms: dotnet
author: seguler
---

# Azure Storage Queue Service - Quick Sample using Pop Receipt and Face API  

This sample demonstrates how to use the new Popreceipt functionality to coordinate updates across two non-transactional resources. In this case it allows a Blob to be published and metadata about the Blob to be written to a table entity. Prior to uploading the blob, a message is enqueued, the updates are performed to blob and table and if successful the message is deleted using its popreceipt. Any messages remaining in the queue need additional work due to the failures experienced, which can be consumed via a backend worker that is not demonstrated in this sample.    

## Sample App: Image Upload and Face Recognition using Face API from Azure Cognitive Services 

In this sample app, we assume that the user has a number of photos in a local folder that needs to be uploaded to Azure as blobs, and using Face API each person's age in the the photos are estimated and added to a table as an entity. We will be tracking the completion of this process in a queue and a backend worker would ideally later on pick the messages from queue to go through failed processes.

### Here is a quick walktrough of the sample:

1. Create the queue if not created already
2. Create the container if not created already
3. Create the table if not created already
4. Find JPG files in ‘testfolder’
5. For each photo, do steps 6-10:
6. Upload a queue message representing the processing of this photo.  If there is a failure while processing this photo, the existence of this queue message will signal that failure to a background cleanup process (not shown here.)
7. Call the Face API to estimate the age of each person in the photo.
8. Store the age information as an entity in the table.
9. Upload the image to a blob if at least one face is detected.
10. If both the blob and the table entity operation succeeded, delete the message from queue using the pop receipt.

Note: This sample uses asynchronous programming model with Task Parallel Library (TPL) to demonstrate how to call the Storage Service using the storage client libraries asynchronous API's. When used in real applications this approach enables you to improve the responsiveness of your application. Calls to the storage service are prefixed by the await keyword. 

## Running this sample

### Instructions:

This sample can be run using either the Azure Storage Emulator that installs as part of the Azure SDK - or by updating the App.Config file with your AccountName and Key. 

#### To run the sample using the Storage Emulator (default option)

1. Start the Azure Storage Emulator (once only) by pressing the Start button or the Windows key and searching for it by typing "Azure Storage Emulator". Select it from the list of applications to start it.
2. Create a folder named 'testfolder' locally (~ bin/Debug) and place a few photos
3. Set breakpoints and run the project using F10. 

#### To run the sample using the Storage Service

1. Open the app.config file and comment out the connection string for the emulator (UseDevelopmentStorage=True) and uncomment the connection string for the storage service (AccountName=[]...)
2. Create a Storage Account through the Azure Portal and provide your [AccountName] and [AccountKey] in the App.Config file. See http://go.microsoft.com/fwlink/?LinkId=325277 for more information
3. Create a folder named 'testfolder' locally (~ bin/Debug) and place a few photos
4. Set breakpoints and run the project using F10. 


## More information

[What is a Storage Account](https://docs.microsoft.com/en-us/azure/storage/common/storage-create-storage-account)

[Cognitive Services Face API](https://azure.microsoft.com/en-us/services/cognitive-services/face/)

[Get started with Azure Queue storage using .NET](https://docs.microsoft.com/en-us/azure/storage/storage-dotnet-how-to-use-queues)

[Queue Service REST API Reference](https://docs.microsoft.com/en-us/rest/api/storageservices/fileservices/queue-service-rest-api)

[Azure Storage Queue Service Samples](https://azure.microsoft.com/en-us/resources/samples/?service=storage&term=queue)
