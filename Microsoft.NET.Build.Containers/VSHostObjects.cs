using Microsoft.Build.Framework;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.NET.Build.Containers;

internal class VSHostObject
{
    IEnumerable<ITaskItem> _hostObject;
    public VSHostObject(IEnumerable<ITaskItem> hostObject)
    {
        _hostObject = hostObject;
    }

    public bool ExtractCredentials(out string username, out string password)
    {
        bool retVal = false;
        username = password = string.Empty;
        if (_hostObject != null)
        {
            ITaskItem credentialItem = _hostObject.FirstOrDefault<ITaskItem>(p => p.ItemSpec == VSMsDeployTaskHostObject.CredentialItemSpecName);
            if (credentialItem != null)
            {
                retVal = true;
                username = credentialItem.GetMetadata(VSMsDeployTaskHostObject.UserMetaDataName);
                if (!string.IsNullOrEmpty(username))
                {
                    password = credentialItem.GetMetadata(VSMsDeployTaskHostObject.PasswordMetaDataName);
                }
            }
        }
        return retVal;
    }
}

internal class VSMsDeployTaskHostObject : ITaskHost, IEnumerable<ITaskItem>
{
    private List<TaskItem> _items;
    public static string CredentialItemSpecName = "MsDeployCredential";
    public static string UserMetaDataName = "UserName";
    public static string PasswordMetaDataName = "Password";
    public static string SkipFileItemSpecName = "MsDeploySkipFile";
    public static string SourceDeployObject = "Source";
    public static string DestinationDeployObject = "Destination";
    public static string SkipApplyMetadataName = "Apply";

    public VSMsDeployTaskHostObject()
    {
        _items = new List<TaskItem>();
    }

    public List<TaskItem> GetTaskItems()
    {
        return _items;
    }

    public void AddCredentialTaskItemIfExists(string userName, string password)
    {
        if (!string.IsNullOrEmpty(userName))
        {
            TaskItem credentialItem = new TaskItem(CredentialItemSpecName);
            ITaskItem2 iTaskItem2 = (credentialItem as ITaskItem2);
            iTaskItem2.SetMetadataValueLiteral(UserMetaDataName, userName);
            iTaskItem2.SetMetadataValueLiteral(PasswordMetaDataName, password);
            _items.Add(credentialItem);
        }
    }

    public void AddFileSkips(List<FileSkipData> fileSkipInfos, /*key is src relative path, value is full destination path*/
        string rootFolderOfFileToPublish)
    {
        foreach (FileSkipData p in fileSkipInfos)
        {
            TaskItem srcSkipRuleItem = new TaskItem(SkipFileItemSpecName);
            srcSkipRuleItem.SetMetadata("ObjectName", p.sourceProvider);
            srcSkipRuleItem.SetMetadata("AbsolutePath", System.Text.RegularExpressions.Regex.Escape(Path.Combine(rootFolderOfFileToPublish, p.sourceFilePath)) + "$");
            srcSkipRuleItem.SetMetadata(SkipApplyMetadataName, SourceDeployObject);
            _items.Add(srcSkipRuleItem);

            TaskItem destSkipRuleItem = new TaskItem(SkipFileItemSpecName);
            destSkipRuleItem.SetMetadata("ObjectName", p.destinationProvider);
            destSkipRuleItem.SetMetadata("AbsolutePath", System.Text.RegularExpressions.Regex.Escape(p.destinationFilePath) + "$");
            destSkipRuleItem.SetMetadata(SkipApplyMetadataName, DestinationDeployObject);
            _items.Add(destSkipRuleItem);
        }
    }

    public IEnumerator<ITaskItem> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_items).GetEnumerator();
    }
}

public class FileSkipData
{
    public string sourceProvider { get; set; }
    public string sourceFilePath { get; set; }
    public string destinationProvider { get; set; }
    public string destinationFilePath { get; set; }
}