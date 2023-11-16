
using System.Collections.Generic;

namespace UmiShun.Arc;

public abstract class ArcEntry
{
	public enum EntryType : uint
	{
		File = 0x0,
		Folder = 0x80000000
	}

	public string Name;
	public abstract EntryType Type {get;}
}

public class ArcFileEntry : ArcEntry
{
	public byte[] Content;

	public override EntryType Type => EntryType.File;
}

public class ArcFolderEntry : ArcEntry
{
	public ArcEntry[] Entries;

	public override EntryType Type => EntryType.Folder;


	public IEnumerable<(string path, ArcFileEntry entry)> GetFilesRecursive()
	{
		foreach (var entry in Entries)
		{
			if (entry is ArcFileEntry fileEntry)
			{
				yield return (fileEntry.Name, fileEntry);
			}
			else if (entry is ArcFolderEntry folderEntry)
			{
				foreach (var (fileName, file) in folderEntry.GetFilesRecursive())
				{
					yield return (entry.Name + "/" + fileName, file);
				}
			}
		}
	}

	public IEnumerable<(string path, ArcFolderEntry entry)> GetFoldersRecursive()
	{
		foreach (var entry in Entries)
		{
			if (entry is ArcFolderEntry folderEntry)
			{
				yield return (entry.Name, folderEntry);
				foreach (var (folderName, folder) in folderEntry.GetFoldersRecursive())
				{
					yield return (entry.Name + "/" + folderName, folder);
				}
			}
		}
	}
}