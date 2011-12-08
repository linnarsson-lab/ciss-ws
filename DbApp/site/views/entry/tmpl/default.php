<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
$searchid = JRequest::getVar('searchid', -1) ;
$menus = JSite::getMenu();
$db =& JFactory::getDBO();
$menu  = $menus->getActive();
$itemid = $menu->id;

function checkProcesses($exenames)
{
    $processcounts = Array();
    foreach ($exenames as $exename)
        $processcounts[$exename] = 0;
    $cmd = "ps ax";
    exec($cmd, $lines, $result);
    foreach ($lines as $line)
    {
        foreach ($processcounts as $exename => $count) {
            if (preg_match("/mono.+$exename/", $line) == 1) {
                $processcounts[$exename] = $count + 1;
            }
        }
    }
     return $processcounts;
}

$waiting = Array();
$query = ' SELECT COUNT(*) FROM #__aaailluminarun WHERE status != "copied" AND status != "cancelled" ';
$db->setQuery($query);
$waiting["Read collector"] = $db->loadResult();
$query = ' SELECT COUNT(*) FROM #__aaafqmailqueue WHERE status = "inqueue" ';
$db->setQuery($query);
$waiting["FastQ mailer"] = $db->loadResult();
$query = ' SELECT COUNT(*) FROM #__aaabackupqueue WHERE status = "inqueue" ';
$db->setQuery($query);
$waiting["Backuper"] = $db->loadResult();
$query = ' SELECT COUNT(*) FROM #__aaaanalysis WHERE status != "ready" AND status != "cancelled" ';
$db->setQuery($query);
$waiting["Analyzer"] = $db->loadResult();

$processnames = Array("Backuper" => "BkgBackuper.exe", "Analyzer" => "ProjectDBProcessor.exe",
                      "FastQ mailer" => "BkgFastQMailer.exe", "Read collector" => "BkgFastQCopier.exe");
$processcounts = checkProcesses(array_values($processnames));
$processlinks = Array();
$processlinks["Backuper"] = "<a href=index.php?option=com_dbapp&view=entry&layout=bupqueue&controller=entry&searchid=" 
                      . $task->id . "&Itemid=" . $itemid . ">Details</a>";
$processlinks["FastQ mailer"] = "<a href=index.php?option=com_dbapp&view=entry&layout=mailqueue&controller=entry&searchid=" 
                      . $task->id . "&Itemid=" . $itemid . ">Details</a>";
$processlinks["Read collector"] = "<a href=index.php?option=com_dbapp&view=illumina-runs&Itemid=" . $itemid . ">Runs</a>";
$processlinks["Analyzer"] = "<a href=index.php?option=com_dbapp&view=analysisresults&Itemid=" . $itemid . ">Samples</a>";

echo "<div><fieldset><legend>Overview of running processes</legend><table>";
echo "<tr><th>Process&nbsp;</th><th>Program&nbsp;</th><th>Running instances&nbsp;</th><th>Tasks in line&nbsp;</th><th></th></tr>\n";

foreach ($processnames as $process => $exename) {
    echo "<tr><td>$process&nbsp;</td><td>$exename&nbsp;</td><td>" . 
	              $processcounts[$exename] . "&nbsp;</td><td>" . 
				  $waiting[$process] ."</td><td>" . $processlinks[$process] . "</td></tr>\n";
}
echo "</table></fieldset></div><br />&nbsp;<br />";
?>
