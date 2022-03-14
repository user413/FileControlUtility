## 1.1.0

New features:
- TransferSettings properties to re-enumerate files renamed with the pattern  &lt;name&gt; (&lt;number&gt;)&lt;extension&gt; and limit their quantity in the destiny directory
- Additional parameters added to the event override methods
- Added method to re-enumerate files given a origin file, in it's own directory
- Comment documentation

Fixed/altered:
- RENAME_DIFFERENT file conflict method algo improvement - binary comparison is done directly with the highest enumerated file
- SpecifiedFileNamesAndExtensions can be null
