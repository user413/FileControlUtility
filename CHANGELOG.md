## 1.3.0

New features:
- Transition to .NET Standard 2.1
- Event handlers added, replacing virtual methods
- Error handling simplified into a single event (OnErrorOccured) and handles a wider group of actions (deletion of files/directories) other than
  the transfer of files
- Full paths and server paths (starting with \\) can be specified for filtering of files / server paths can be specified for filtering of directories

## 1.2.1

Fixed/altered:
- Allow transfer for paths starting with double "\\" or "//" (servers)

## 1.2.0

New features:
- Directory exclusion in transfers (full and relative paths)
- TransferSettings constructor with properties

Fixed/altered:
- Adjustment/formatting of paths - fix
- DeleteUncommonFiles with an non existing destiny path - fix
- Removed exception message portion from error event messages - fix

## 1.1.0

New features:
- TransferSettings properties to re-enumerate files renamed with the pattern  &lt;name&gt; (&lt;number&gt;)&lt;extension&gt; and limit their quantity in the destiny directory
- Additional parameters added to the event override methods
- Added method to re-enumerate files given a origin file, in it's own directory
- Comment documentation

Fixed/altered:
- RENAME_DIFFERENT file conflict method algo improvement - binary comparison is done directly with the highest enumerated file
- SpecifiedFileNamesAndExtensions can be null
