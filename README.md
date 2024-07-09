# ChromaDB.Client

ChromaDB.Client is a C# cross-platform library for communication with ChromaDB Vector database. ChromaDB is a fast and scalable database that uses vector similarity search to enable complex queries on high-dimensional data.

With ChromaDB.Client, you can easily connect to a ChromaDB instance, create and manage collections, perform CRUD operations on the data in the collections, and execute other available operations such as nearest neighbor search, aggregation, and filtering.

**This library is still in early development and is not ready for production use by any means.**

## Features

- [x] Basic connection and authentication (partially done)
- [x] Collection creation (partially done)
- [ ] Collection deletion
- [x] Collection retrieval and modification (partially done)
- [ ] Document insertion, deletion, and update
- [ ] Document retrieval by ID or filter
- [ ] Vector similarity search with optional parameters
- [ ] Aggregation and grouping operations
- [ ] Batch operations and transactions
- [ ] Async and await support
- [ ] Logging and error handling

## Build status

![CI](https://github.com/ssone95/ChromaDB.Client/actions/workflows/ci.yml/badge.svg)

## Important notice
### **This project is in no way associated with, or supported/funded by the original authors of ChromaDB. It's solely developed as a hobby project, and with sole purpose of connecting to the database itself, and making it more connectable with the rest of .NET ecosystem! All rights to ChromaDB go to the respective authors of the said software!**

By using the provided library, you are also accepting the following terms provided by @chroma team: https://www.trychroma.com/terms
