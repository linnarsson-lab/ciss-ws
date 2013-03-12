<?php
defined('_JEXEC') or die('Restricted access');
require_once ('strt2Qsingle.php');
// createqlucore.php
?>


<?php
  echo "<h1>Chose data for Qlucore data file generation</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
//  $sortKey = JRequest::getVar('sortKey', "");
  $itemid = $menu->id;

  echo "<div class='analysis'><fieldset>
         <legend><nobr>Decide what analysis result to use</nobr>
         </legend>
         <table>
         <tr>
          <th>&nbsp;</th>
          <th>Sample</th>
          <th>Cmnt</th>
         </tr>";

  foreach ($this->items as $result) {
    if ($result->status == "cancelled")
        continue;
    echo "<tr>";
    echo "<td>" . $result->resultspath . "</td>";
    echo "<td>" . $result->extraction_version . "</td>";
    echo "<td>" . $result->annotation_version . "</td>";
    echo "</tr>";
  }
  echo "</table>";

?>
