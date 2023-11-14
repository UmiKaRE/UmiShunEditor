
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
}