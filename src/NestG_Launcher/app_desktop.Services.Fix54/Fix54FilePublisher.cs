using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54FilePublisher : IFix54FilePublisher
{
	public async Task ReplaceAsync(string source, string destination, string expectedDestinationHash, CancellationToken cancellationToken)
	{
		Fix54Paths.EnsureNoReparse(source);
		Fix54Paths.EnsureNoReparse(destination);
		string temporary = destination + ".nestg-fix54-" + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			await using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
			{
				await using FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.WriteThrough | FileOptions.Asynchronous);
				await input.CopyToAsync(output, cancellationToken);
				await output.FlushAsync(cancellationToken);
				output.Flush(flushToDisk: true);
			}
			cancellationToken.ThrowIfCancellationRequested();
			Fix54Paths.EnsureNoReparse(destination);
			if (await Fix54Paths.HashAsync(destination, cancellationToken) != expectedDestinationHash)
			{
				throw new Fix54Exception("EXE game đã thay đổi trước khi cập nhật. FIX54 không ghi đè thay đổi đó.");
			}
			cancellationToken.ThrowIfCancellationRequested();
			File.Move(temporary, destination, overwrite: true);
		}
		finally
		{
			if (File.Exists(temporary))
			{
				File.Delete(temporary);
			}
		}
	}
}
