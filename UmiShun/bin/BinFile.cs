

using System.IO;

namespace UmiShun.BinFileList;

public class BinFile
{
	public int FileCount;
	public byte[][] Files;

	public static bool TryReadBinFile(BinaryReader reader, out BinFile binFile)
	{
		try
		{
			binFile = new BinFile();

			binFile.FileCount = reader.ReadInt32();
			binFile.Files = new byte[binFile.FileCount][];

			for (int i = 0; i < binFile.FileCount; i++)
			{
				int fileSize = reader.ReadInt32();
				int fileOffset = reader.ReadInt32();
				long previousPos = reader.BaseStream.Position;
				reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
				binFile.Files[i] = reader.ReadBytes(fileSize);
				reader.BaseStream.Seek(previousPos, SeekOrigin.Begin);
			}

			return true;
		}
		catch
		{
			binFile = null;
			return false;
		}
	}

	public static bool TryReadBinFile(byte[] content, out BinFile binFile)
	{
		using (var stream = new MemoryStream(content))
		{
			using (var reader = new BinaryReader(stream))
			{
				return TryReadBinFile(reader, out binFile);
			}
		}
	}
}