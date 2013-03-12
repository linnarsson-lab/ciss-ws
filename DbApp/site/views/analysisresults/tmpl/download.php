<?php
defined('_JEXEC') or die('Restricted access');
require_once ('strt2Qsingle.php');

echo "<h1>Analysis Result Download for Qlucore</h1>";
$menus = &JSite::getMenu();
$menu  = $menus->getActive();
$sortKey = JRequest::getVar('sortKey', "");
$itemid = $menu->id;

$analysisid = JRequest::getVar("analysisid", "");
foreach ($this->items as $result) {
  if ($result->id == $analysisid) {
    $filePath = $result->resultspath;
    $gedataFiles = glob($filePath . "/*.gedata");
    if (count($gedataFiles) > 0) {
      $shortName = $qlucoreFile = basename($gedataFiles[0]);
      $qout = "/srv/www/htdocs/joomla16/tmp/" . $qlucoreFile;
      copy($gedataFiles[0], $qout);
    } else {
      $dirs = explode("/", $filePath);
      $sampleId = $dirs[count($dirs) - 2];
      $countType = "RPM";
      $testPath = $filePath . "/" . $sampleId . "_" . $countType . ".tab";
      if (!file_exists($testPath))
        $countType = "RPKM";
      $nameHead = $sampleId . "_" . $countType;
      $shortName = $nameHead . ".tab";
      $fileName = $filePath . "/" . $shortName;
      $qlucoreFile = $nameHead . ".gedata";
      $qout = toQlucore($fileName);
    }
  }
}

echo "<br /><br />Right-click this link to the Qlucore data file and save it on your computer:<br /><br />";
echo "<a href=http://192.168.1.12/joomla16/tmp/" . $qlucoreFile . ">" . $qlucoreFile . "</a>";
?>

<script language="javascript">
  FileInputStream fileToDownload = new FileInputStream("<?php echo $qout; ?>");
  ServletOutputStream output = response.getOutputStream();
  response.setContentType("application/text-plain");
  response.setHeader("Content-Disposition", "attachment; filename=<?php echo $shortName; ?>");
  response.setContentLength(fileToDownload.available());
  int c;
  while ((c = fileToDownload.read()) != -1) {
    output.write(c);
  }
  output.flush();
  output.close();
  fileToDownload.close();

  fileToDownload();
 </script>

