<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  foreach ($this->illuminaruns as $lane) {
    $irilluminarunid = $lane->illuminarunid;
    $irdbid = $lane->id;
    $irrunno = $lane->runno;
    $ircopystatus = $lane->status;
  }

#######       READ in directory inof from cofig file    ######################################
    $xmlfile = JPATH_COMPONENT_ADMINISTRATOR.DS.'config.txt';
    $string = rtrim(file_get_contents($xmlfile));
    $words = preg_split("/\s/", $string);
    $xmlstring = file_get_contents($words[1]);
    preg_match("/<RunsFolder\>(.*)<\/RunsFolder>/", $xmlstring, $matches);
    $runsfolder = $matches[1];
    preg_match("/<ProjectsFolder\>(.*)<\/ProjectsFolder>/", $xmlstring, $matches);
    $projectsfolder = $matches[1];
    preg_match("/<UploadsFolder>(.*)<\/UploadsFolder>/", $xmlstring, $matches);
    $uploadsfolder = $matches[1];
    $uploadslinkfolder = "/uploads/" ;#. $uploadsfolder;
##############################################################################################

?>
<script type="text/javascript">
function updateSelect(i)
{
  if (document.getElementById("lanesel" + i).checked) {
    document.getElementById("email" + i).disabled = false;
  } else {
    document.getElementById("email" + i).disabled = true;
  }
  var c = 0;
  for (l = 1; l <= 8; l++) {
    if (document.getElementById("lanesel" + i).checked) c++;
  }
  document.getElementById("submitbutton").disabled = (c == 0); 
  return true;
}

function validateForm()
{
	if (document.getElementById("submitbutton").value == "Cancel")
		return true;
	var filter = /^([a-zA-Z0-9_\.\-])+\@(([a-zA-Z0-9\-])+\.)+([a-zA-Z0-9]{2,4})+$/;
	for (i = 1; i <= 8; i++) {
		if (document.getElementById("lanesel" + i).checked) {
				var email = document.getElementById("email" + i).value;
			if (!filter.test(email)) {
				alert("The email address " + email + " is invalid!");
				return false;
			}
		}
	}
	adminForm.submit();
}
</script>

<form action="<?php echo JText::_('?option=com_dbapp&view=illuminarun&layout=savemails&id='.(int) $irrunno); ?>" method="post" name="adminForm" id="admin-form" class="form-validate">

<?php
    echo "<h1>Email FastQ read files from run $irilluminarunid </h1>";

    echo "<div class='lanes'><fieldset>
           <legend>Select lanes:&nbsp;</legend>";
    echo "<table>
           <tr>
            <th>Lane</th>
            <th>SampleId&nbsp;</th>
            <th>Batch</th>
            <th>P.I.&nbsp;</th>
            <th>Contact&nbsp;</th>
            <th>Comment</th>
            <th><nobr>Email address&nbsp;</nobr></th>
            <th>Select</th>
           </tr>";
  $boxid = "";
  foreach ($this->illuminaruns as $lane) {
    echo "<tr>
            <td>" . $lane->laneno . "</td>
            <td><nobr>";
    if ($lane->Sid === null)
      echo " ? ";
    else if ($lane->Sid === "0")
      echo "EMPTY";
    else
      echo "<a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
            . $lane->projectid . "&Itemid=" . $itemid . ">" . $lane->plateid . "</a>&nbsp;";
    echo "  </nobr></td>
            <td>";
    if ($lane->Sid === "0" || $lane->Sid === null)
      echo " ";
    else
      echo $lane->batchtitle;
    echo    "&nbsp;</td>
            <td>" . $lane->pi . "&nbsp;</td>
            <td>" . $lane->contactperson . "&nbsp;</td>
            <td>" . $lane->Lcomment . "</td>";
    if ($ircopystatus == "copied" && $lane->Sid > 1) {
      $boxid = "lanesel" . $lane->laneno;
      $emailid = "email" . $lane->laneno;
      echo '<td><input type="text" name="' . $emailid . '" id="' . $emailid 
           . '" value="' . $lane->contactemail . '" disabled="disabled" />&nbsp;</td>';
      echo '<td><input type="checkbox" name="' . $boxid . '" id="' . $boxid
           . '" onClick="return updateSelect(' . $lane->laneno . ');" /></td>';
    echo "</tr>";
    }
  }
?>
    </table>
  </fieldset>
</div>
<br/>
<input type="hidden" name="runno" value="<?php echo $irrunno ?>" />
<?php
  if ($boxid == "") {
    echo "<p><b>No valid lanes with fastq files ready to send were found!</b></p>";
  } else {
    echo '<input type="Submit" name="Submit" id="submitbutton" value="Put selected lanes in mail queue" onclick="validateForm(); return false;" disabled="disabled" />';
  }
?>
<input type="Submit" name="Submit" value="Cancel" />
<?php
    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<a href=index.php?option=com_dbapp&view=illuminaruns&Itemid="
         . $itemid . ">Return to list of Illumina runs</a><br/>&nbsp;<br/>";
    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $item->id . '" />';
    }

?>
    <input type="hidden" name="task" value="savemail" />
    <?php echo JHtml::_('form.token'); ?>
</form>

