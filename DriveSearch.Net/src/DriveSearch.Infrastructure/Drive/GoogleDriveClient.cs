using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;

namespace DriveSearch.Infrastructure.Drive;

/// <summary>
/// Client for interacting with Google Drive API.
/// Handles file listing, downloading, and format conversion.
/// </summary>
public class GoogleDriveClient
{
    private readonly GoogleAuthService _authService;

    /// <summary>
    /// Supported MIME types for indexing.
    /// </summary>
    public static readonly string[] SupportedMimeTypes =
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // DOCX
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",       // XLSX
        "application/vnd.openxmlformats-officedocument.presentationml.presentation", // PPTX
        "application/vnd.google-apps.document",    // Google Docs
        "application/vnd.google-apps.spreadsheet", // Google Sheets
        "application/vnd.google-apps.presentation" // Google Slides
    };

    public GoogleDriveClient(GoogleAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Lists all files from Google Drive (including shared drives).
    /// Filters by supported MIME types.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Drive file metadata</returns>
    public async Task<List<DriveFile>> ListAllFilesAsync(CancellationToken cancellationToken = default)
    {
        var service = await _authService.CreateDriveServiceAsync(cancellationToken: cancellationToken);

        var files = new List<DriveFile>();
        string? pageToken = null;

        // Build MIME type filter query
        var mimeTypeQuery = string.Join(" or ", SupportedMimeTypes.Select(mt => $"mimeType='{mt}'"));
        var query = $"({mimeTypeQuery}) and trashed=false";

        do
        {
            var request = service.Files.List();
            request.Q = query;
            request.Fields = "nextPageToken, files(id, name, mimeType, size, createdTime, modifiedTime, owners, parents)";
            request.PageSize = 100;
            request.PageToken = pageToken;
            request.SupportsAllDrives = true;
            request.IncludeItemsFromAllDrives = true;

            var response = await request.ExecuteAsync(cancellationToken);

            if (response.Files != null)
            {
                foreach (var file in response.Files)
                {
                    files.Add(new DriveFile
                    {
                        Id = file.Id,
                        Name = file.Name,
                        MimeType = file.MimeType,
                        Size = file.Size ?? 0,
                        CreatedTime = file.CreatedTime,
                        ModifiedTime = file.ModifiedTime,
                        OwnerName = file.Owners?.FirstOrDefault()?.DisplayName,
                        ParentId = file.Parents?.FirstOrDefault()
                    });
                }
            }

            pageToken = response.NextPageToken;
        }
        while (pageToken != null);

        return files;
    }

    /// <summary>
    /// Downloads a file from Google Drive.
    /// For Google Docs/Sheets/Slides, exports as PDF.
    /// </summary>
    /// <param name="fileId">Google Drive file ID</param>
    /// <param name="mimeType">File MIME type</param>
    /// <param name="destinationPath">Local path to save file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task DownloadFileAsync(
        string fileId,
        string mimeType,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var service = await _authService.CreateDriveServiceAsync(cancellationToken: cancellationToken);

        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);

        // Google Workspace files need to be exported
        if (IsGoogleWorkspaceFile(mimeType))
        {
            // Export as PDF
            var exportRequest = service.Files.Export(fileId, "application/pdf");
            await exportRequest.DownloadAsync(fileStream, cancellationToken);
        }
        else
        {
            // Native files can be downloaded directly
            var getRequest = service.Files.Get(fileId);
            await getRequest.DownloadAsync(fileStream, cancellationToken);
        }
    }

    /// <summary>
    /// Returns a dictionary mapping folder ID → folder name for all non-trashed folders.
    /// </summary>
    public async Task<Dictionary<string, string>> GetFolderNamesAsync(CancellationToken cancellationToken = default)
    {
        var service = await _authService.CreateDriveServiceAsync(cancellationToken: cancellationToken);
        var result = new Dictionary<string, string>();
        string? pageToken = null;

        do
        {
            var request = service.Files.List();
            request.Q = "mimeType='application/vnd.google-apps.folder' and trashed=false";
            request.Fields = "nextPageToken, files(id, name)";
            request.PageSize = 1000;
            request.PageToken = pageToken;
            request.SupportsAllDrives = true;
            request.IncludeItemsFromAllDrives = true;

            var response = await request.ExecuteAsync(cancellationToken);

            if (response.Files != null)
                foreach (var folder in response.Files)
                    result[folder.Id] = folder.Name;

            pageToken = response.NextPageToken;
        }
        while (pageToken != null);

        return result;
    }

    /// <summary>
    /// Checks if a MIME type is a Google Workspace file (Docs, Sheets, Slides).
    /// </summary>
    private static bool IsGoogleWorkspaceFile(string mimeType)
    {
        return mimeType.StartsWith("application/vnd.google-apps.");
    }
}

/// <summary>
/// Represents metadata for a Google Drive file.
/// </summary>
public class DriveFile
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string MimeType { get; set; }
    public long Size { get; set; }
    public DateTime? CreatedTime { get; set; }
    public DateTime? ModifiedTime { get; set; }
    public string? OwnerName { get; set; }
    public string? ParentId { get; set; }
}
