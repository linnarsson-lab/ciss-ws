<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  echo "<h1> Sequencing primers </h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $newlink = "<a href=index.php?option=com_dbapp&view=sequencingprimer&layout=edit&controller=sequencingprimer&searchid=0&Itemid=" . $itemid . " >&nbsp;Add&nbsp;new&nbsp;primer&nbsp;</a>";
  echo "<div class='sequencingprimer'>
          <fieldset>
            <legend> $newlink </legend>
          <table>
            <tr>
              <th colspan='2'></th>
              <th>Sequencing&nbsp;primer&nbsp;</th>
              <th>Sequence&nbsp;</th>
              <th>Comment</th>
            </tr>";
  foreach ($this->items as $primer) {
    echo "<tr>";
    $primerlink = "&nbsp; <a href=index.php?option=com_dbapp&view=sequencingprimer&layout=sequencingprimer&controller=sequencingprimer&searchid=" 
           . $primer->id . "&Itemid=" . $itemid . ">view</a> &nbsp;";
    $editlink = "&nbsp; <a href=index.php?option=com_dbapp&view=sequencingprimer&layout=edit&controller=sequencingprimer&searchid=" 
           . $primer->id . "&Itemid=" . $itemid . ">edit</a> &nbsp;";
    echo "<td>" . $primerlink . "</td>";
    echo "<td>" . $editlink . "</td>";
    echo "<td>" . $primer->primername . "&nbsp;</td>"; 
    echo "<td>" . $primer->sequence . "&nbsp;</td>";
    $comment = $primer->comment;
    if (strlen($comment) > 7)
      echo "<td>&nbsp;" . JHTML::tooltip($comment) . "&nbsp;</td></tr>";
    else
      echo "<td> $comment &nbsp;</td></tr>";
//    echo "<td> $primer->user &nbsp; " ;
//    echo $primer->time . "</td>";
    echo "</tr>";
  }
  echo "</table></fieldset></div><br />&nbsp;<br />";

?>

