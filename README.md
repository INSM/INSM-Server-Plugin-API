# INSM Server Plugin API

A server plugin can provide alternative implementation to certain functions. In some case a default implementation exists that will be overridden if a server plugin exists. The functions a server plugin may implement are:

* ICDNServerPlugin
* IEffectiveDataSetFilterServerPlugin
* IEnhancedAccessRightServerPlugin
* IFileStoreServerPlugin
* IPreviewFileServerPlugin
* ISendMailServerPlugin
* IUserStoreServerPlugin
