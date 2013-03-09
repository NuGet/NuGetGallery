## Adding new tests:

   * Add a new unit test or web test based on the feature.
   
   * For unit tests, extend the test from "GalleryTestBase" class. This is a base class which does some initialization and cleanup.
   
   * Make sure that Urls are picked up from "UrlHelper" and any const strings are added to "Constants".
   
   * For validations, check if the helper you are looking for is present in the helper project. If not add a new one.
   
   * Even for web tests, try to make the validations as much client SDK based as possible. Example : PackagesPageTest.
   
   * Add the tests to appropriate test suite in the NugetGallery.FunctionalTests.vsmdi file.
   


## Running tests locally :

   * Execute "RunTests.cmd" from the scripts folder to run all tests. [this would run all happy path tests which doesn’t require read-only mode or SQL to be down).
     RunTests.cmd takes an input parameter to set the Gallery Url to point to.
  
     Example: RunTests.cmd https://preview.nuget.org/ would run the tests against preview.
     By default it points to the bvt test environment.

   * Execute "RunSpecificTests.cmd" to run specific tests based on a criteria.
  
     Example: To run all tests which has "Download" in the test name run the below command :
     RunSpecificTests.cmd Download.
  
 
   * To run tests from VS IDE, use Test Explorer to run unit tests. For web tests, open the test in IDE code editor and use "Run coded web performance test" context menu. Same step for debugging tests as well.



