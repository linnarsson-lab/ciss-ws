<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
$db =& JFactory::getDBO();
$mainframe = JFactory::getApplication();

$lim  = $mainframe->getUserStateFromRequest("$option.limit", 'limit', 14, 'int');
   //I guess getUserStateFromRequest is for session or different reasons
$lim0 = JRequest::getVar('limitstart', 0, '', 'int');

$db->setQuery(' SELECT  SQL_CALC_FOUND_ROWS l.id as id, r.id as aaailluminarunid, illuminarunid,
                        b.id as aaasequencingbatchid, laneno, l.molarconcentration, yield,
                        l.comment as comment, l.user as user, l.time as time, title, plateid, rundate
               FROM #__aaalane l 
               LEFT JOIN #__aaailluminarun r ON l.#__aaailluminarunid = r.id
               LEFT JOIN #__aaasequencingbatch b ON l.#__aaasequencingbatchid = b.id
               LEFT JOIN #__aaaproject p ON b.#__aaaprojectid = p.id
               ORDER BY l.id ',$lim0, $lim);
$rL=&$db->loadAssocList();
//if (empty($rL)) {$jAp->enqueueMessage($db->getErrorMsg(),'error'); return;}  
//else {
////Here the beauty starts
$db->setQuery('SELECT FOUND_ROWS();');  //no reloading the query! Just asking for total without limit
jimport('joomla.html.pagination');
$pageNav = new JPagination( $db->loadResult(), $lim0, $lim );
//foreach($rL as $r) {
//your display code here
//}

  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $newlink = "<a href=index.php?option=com_dbapp&view=lane&layout=edit&controller=lane&searchid=0&Itemid="
             . $itemid . " >&nbsp;Add&nbsp;new&nbsp;lane&nbsp;</a>";
  echo "<h1>All lanes list</h1><br />
          <form action=" . JRoute::_('index.php?option=com_dbapp') . " method='post' name='adminForm'>
            <table>
              <legend>$newlink</legend>
              <tr>
                <th></th>
                <th></th>
                <th>Plate&nbsp;id&nbsp;</th>
                <th>Project&nbsp;title&nbsp;</th>
                <th>Run&nbsp;id&nbsp;</th>
                <th>Run&nbsp;date&nbsp;</th>
                <th>Lane&nbsp;no&nbsp;</th>
                <th>Conc&nbsp;[pM]&nbsp;</th>
                <th>Yield&nbsp;</th>
                <th>&nbsp;Latest&nbsp;edit&nbsp;</th>
              </tr>";

  foreach ($this->lanes as $lane) {
    $lanelink = "<a href=index.php?option=com_dbapp&view=lane&layout=lane&controller=lane&searchid=" 
           . $lane->id . "&Itemid=" . $itemid . ">&nbsp;view&nbsp;</a>";
    $editlink = "<a href=index.php?option=com_dbapp&view=lane&layout=edit&controller=lane&searchid=" 
           . $lane->id . "&Itemid=" . $itemid . ">&nbsp;edit&nbsp;</a>";
    echo "    <tr>
                <td>$lanelink</td>
                <td>$editlink</td>
                <td>" . $lane->plateid . "</td>
                <td>" . $lane->title . "</td>
                <td>" . $lane->illuminarunid . "</td>
                <td><nobr>" . $lane->rundate . "</nobr></td>
                <td>" . $lanelink . $lane->laneno . "</td>
                <td>" . $editlink . $lane->molarconcentration . "</td>
                <td>" . $lane->yield . "</td>
                <td><nobr> &nbsp; " . $lane->user . " &nbsp;" . $lane->time . "</nobr></td>
              </tr>";
  }
  echo "<tr><td colspan='8'>" . $pageNav->getListFooter() . "<td></tr>";
  echo "</table><br />";
  echo "<input type='hidden' name='view' value='default' />";
  echo JHtml::_('form.token');
  echo "</form>";

?>
