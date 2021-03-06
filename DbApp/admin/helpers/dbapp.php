<?php
defined('_JEXEC') or die('Restricted access');

abstract class DbAppHelper {

	public static function addSubmenu($submenu) {
    JSubMenuHelper::addEntry(JText::_('Clients'), 'index.php?option=com_dbapp', $submenu == 'message');
    JSubMenuHelper::addEntry(JText::_('Client categories'), 'index.php?option=com_categories&view=categories&extension=com_dbapp', $submenu == 'categories');
    JSubMenuHelper::addEntry(JText::_('Projects'), 'index.php?option=com_dbapp&view=projects', $submenu == 'message');
    JSubMenuHelper::addEntry(JText::_('Contacts'), 'index.php?option=com_dbapp&view=contacts', $submenu == 'message');
    JSubMenuHelper::addEntry(JText::_('Managers'), 'index.php?option=com_dbapp&view=managers', $submenu == 'message');
    JSubMenuHelper::addEntry(JText::_('Seq Batches'), 'index.php?option=com_dbapp&view=sequencingbatches', $submenu == 'message');
    JSubMenuHelper::addEntry(JText::_('Lanes'), 'index.php?option=com_dbapp&view=lanes', $submenu == 'message');
    JSubMenuHelper::addEntry(JText::_('Illumina Runs'), 'index.php?option=com_dbapp&view=illuminaruns', $submenu == 'message');
    JSubMenuHelper::addEntry(JText::_('Sequencing Primers'), 'index.php?option=com_dbapp&view=sequencingprimers', $submenu == 'message');
		// set some global property
		$document = JFactory::getDocument();
		$document->addStyleDeclaration('.icon-48-helloworld {background-image: url(../media/com_dbapp/images/tux-48x48.png);}');
		if ($submenu == 'categories') 
		{
			$document->setTitle(JText::_('DbApp administration categories'));
		}
	}

	public static function getActions($messageId = 0)	{
		$user	= JFactory::getUser();
		$result	= new JObject;

		if (empty($messageId)) {
			$assetName = 'com_dbapp';
		} else {
			$assetName = 'com_dbapp.message.'.(int) $messageId;
		}
//echo JError::raiseWarning(500, 'Assetname < '. $assetName . ' >');

		$actions = array(
			'core.admin', 'core.manage', 'core.create', 'core.edit', 'core.delete'
		);

		foreach ($actions as $action) {
			$result->set($action,	$user->authorise($action, $assetName));
		}

		return $result;
	}
}
