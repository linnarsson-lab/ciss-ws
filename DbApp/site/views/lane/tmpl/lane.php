<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $lane = $this->lane;
  $editlink = "<a href=index.php?option=com_dbapp&view=lane&layout=edit&controller=lane&searchid=" 
           . $lane->id . "&Itemid=" . $itemid . ">Edit this record</a>";
    echo "<H1>Lanes are defined by Illumina/Sequencing run/lane number</H1>";
    echo "<H1> Illumina run id: " . $lane->illuminarunid . " &nbsp; &nbsp; &nbsp; &nbsp; Lane no: " . $lane->laneno . " &nbsp; &nbsp; &nbsp; " . $editlink . "</H1><BR />";
    echo "<table><tr><td>Sequencing&nbsp;batch&nbsp;DB&nbsp;id:&nbsp;</td><td>" . $lane->aaasequencingbatchid . "</td></tr>";
    echo "<tr><td>Conc&nbsp;[pM]:&nbsp;</td><td>" . $lane->molarconcentration . "</td></tr>";
    echo "<tr><td>Cycle&nbsp;count:&nbsp;</td><td>" . $lane->cycles . "</td></tr>";
    echo "<tr><td>Yield:&nbsp;</td><td>" . $lane->yield . "</td></tr>";
    echo "<tr><td>Comment:&nbsp;</td><td>" . $lane->comment . "</td></tr></table>";
    echo "<hr />";
    echo "<table><tr><td>User:&nbsp;</td><td>" . $lane->user . "";
    echo "<tr><td>Latest&nbsp;edit:&nbsp;</td><td>" . $lane->time . "</td></tr></table>";
    echo "<hr />";
    echo "<br />";
    echo "<a href=index.php?option=com_dbapp&view=lanes&Itemid=" . $itemid . ">Return to lanes list</a>";
?>
