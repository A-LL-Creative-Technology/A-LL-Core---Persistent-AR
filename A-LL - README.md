# Persistent Augmented Reality Core package of A-LL Creative Technology mobile Unity Framework

## Installation

To use this package in a unity project :

1. Clone the repository in local directory.
2. In Unity, open Window > Package Manager and "Add Package from git url ..." and insert this URL https://github.com/A-LL-Creative-Technology/A-LL-Core---Persistent-AR.git.
3. Add the following third-party packages from the Package Manager
    1. AR Foundation
    2. ARCore XR Plugin
    3. ARKit XR Plugin
    4. ARCore Extensions by following this link (https://developers.google.com/ar/develop/unity-arf/getting-started-extensions)
        - Import samples in the description of the package in the Package Manager
        - Add an assembly reference in Samples/ARCore Extensions/1.30.0/Persistent Cloud Anchor Sample/Scripts named "ARCore Extensions" and link it to "AR Core - Persistent AR"
    5. Add Nice Vibrations by Lofelt 
4. Update project settings as follows :
    1. XR Plug-in Management
        - Check ARKit
        - CHeck ARCore
            - Set Requirement as required
            - Set Depth as optional
        - ArCore Extensions
            - Check iOS Support Enabled
            - Set ANdroid Authentication Strategy as Keyless
            - Set iOS Authentication Strategy as Authentication Token
5. Add Assembly Reference "Samples Main" in the folder Samples pointing to Google.XR.ARCoreExtensions
