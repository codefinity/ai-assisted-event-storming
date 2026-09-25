namespace EventStorming.Persistence.MongoDb;

/// <summary>
/// The connection string must point at a replica set (a single node is enough): multi-document
/// transactions - used by board import, duplication and cascading deletes - need one.
/// </summary>
public sealed record MongoOptions(string ConnectionString, string Database);
