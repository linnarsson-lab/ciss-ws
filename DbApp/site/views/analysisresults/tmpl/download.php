<?php
defined('_JEXEC') or die('Restricted access');
require_once ('strt2Qsingle.php');
?>


<?php
  echo "<h1>Analysis Result Download for Qlucore</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $sortKey = JRequest::getVar('sortKey', "");
  $itemid = $menu->id;

if (1) {
  $analysisid = JRequest::getVar("analysisid", "");
  foreach ($this->items as $result) {
    if ($result->id == $analysisid) {
      $filePath = $result->resultspath;
      $dirs = explode("/", $filePath);
      $fileName = $filePath . "/" . $dirs[3] . "_RPM.tab";
      $shortName = $dirs[3] . "_RPM.tab";
      $qlucoreFile = $dirs[3] . "_RPM.gedata";
    }
  }

  $qout = toQlucore($fileName);

//  echo $qout;
  echo "<br /><br />To download right-click this link and save the file to your computer <a href=http://192.168.1.12/joomla16/tmp/" . $qlucoreFile . ">" . $qlucoreFile . "</a>";
}

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

