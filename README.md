---
services: storage, cognitive-services
platforms: dotnet
author: seguler
---

# Azure Storage Queue Service - Quick Sample using Pop Receipt and Face API  

Azure Queue Service Pop Receipt Sample - The Queue Service provides reliable messaging for workflow processing and for communication between loosely coupled components of cloud services. This sample demonstrates how to perform tasks using the pop receipt value returned by the service after an add message call.    

## Sample App: Image Upload and Face Recognition using Face API from Azure Cognitive Services 

Here is a quick walktrough of the sample:

1. Create the queue if not created already
2. Create the container if not created already
3. Find JPG files in ‘testfolder’
4. Call Face API to estimate the age of each person from the photos
5. Upload the blob
6. Store the age information as an entity in the table
7. If both the blob and the table entity exists, delete the message from queue using the pop receipt 

Note: This sample uses the .NET 4.5 asynchronous programming model to demonstrate how to call the Storage Service using the storage client libraries asynchronous API's. When used in real applications this approach enables you to improve the responsiveness of your application. Calls to the storage service are prefixed by the await keyword. 

## Running this sample

Instructions:

This sample can be run using either the Azure Storage Emulator that installs as part of the Azure SDK - or by updating the App.Config file with your AccountName and Key. 

To run the sample using the Storage Emulator (default option)

1. Start the Azure Storage Emulator (once only) by pressing the Start button or the Windows key and searching for it by typing "Azure Storage Emulator". Select it from the list of applications to start it.
2. Create a folder named 'testfolder' and place a few photos
3. Set breakpoints and run the project using F10. 

To run the sample using the Storage Service

1. Open the app.config file and comment out the connection string for the emulator (UseDevelopmentStorage=True) and uncomment the connection string for the storage service (AccountName=[]...)
2. Create a Storage Account through the Azure Portal and provide your [AccountName] and [AccountKey] in the App.Config file. See http://go.microsoft.com/fwlink/?LinkId=325277 for more information
3. Create a folder named 'testfolder' and place a few photos
4. Set breakpoints and run the project using F10. 


## More information

[What is a Storage Account](http://azure.microsoft.com/en-us/documentation/articles/storage-whatis-account/)

[Cognitive Services Face API](https://www.microsoft.com/cognitive-services/en-us/face-api)

[Get started with Azure Queue storage using .NET] (https://docs.microsoft.com/en-us/azure/storage/storage-dotnet-how-to-use-queues)

[Queue Service REST API Reference](https://docs.microsoft.com/en-us/rest/api/storageservices/fileservices/queue-service-rest-api)

[Azure Storage Queue Service Samples] (https://azure.microsoft.com/en-us/resources/samples/?service=storage&term=queue)
